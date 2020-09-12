using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using commons.services.Saga;
using commons.services.Utils;

namespace commons.services.Sagas
{
    public interface ISimpleSaga 
    {
        SagaDefinition GetSagaDefinition();
        SagaWorker GetSagaWorker();

        void OnSagaRolledBack(string sagaId, SagaData sagaData);
        Task DoCompensationOfSaga(string sagaId, SagaData form);
    }

    public abstract class SimpleSaga<FormType> : ISimpleSaga where FormType : class, SagaData
    {
        private readonly ILogger _logger;
        private readonly ISagaResolver _sagaResolver;
        public SimpleSaga(ILogger logger, ISagaResolver sagaResolver)
        {
            this._logger = logger;
            this._sagaResolver = sagaResolver;
            // saga data types改成在这里静态方法注册，把FormType注册，用来找到所有的saga data types
            SagaGlobal.BindSagaDataType(typeof(FormType));

            AutoBindBranchServices();
        }

        private void AutoBindBranchServices()
        {
            // saga的实现类的各满足要求的方法，以及compensable方法（如果还没注册过的话）自动BindBranch，
            // 这样避免具体实现类中手动写
            var sagaTypeInfo = this.GetType();
            var methods = sagaTypeInfo.GetMethods();
            foreach(var method in methods)
            {
                if(!method.IsPublic)
                {
                    continue;
                }
                if(method.IsStatic)
                {
                    continue;
                }
                if(method.ReturnType != typeof(Task))
                {
                    continue;
                }
                var methodParams = method.GetParameters();
                if(methodParams.Length!=1 || methodParams[0].ParameterType!=typeof(FormType))
                {
                    continue;
                }
                Func<FormType, Task> func = delegate (FormType form)
                {
                    return method.Invoke(this, new object[] { form }) as Task;
                };
                // var action = DelegateBuilder.BuildDelegate<Func<FormType, Task>>(method, sagaTypeInfo);
                var serviceKey = _sagaResolver.GetServiceKey(sagaTypeInfo, method.Name);
                _sagaResolver.BindBranch<FormType>(serviceKey, func);
                _logger.LogInformation($"saga resolver binded action {func}");
                // 如果有compensable注解，要求方法在本类中，并且是public方法
                var compensableAttr = MethodUtils.GetDeclaredAttribute<Compensable>(method, typeof(Compensable));
                if(compensableAttr!=null)
                {
                    var compensableMethodName = compensableAttr.ActionName;
                    var compensableMethod = MethodUtils.GetMethod(sagaTypeInfo, compensableMethodName);
                    if(compensableMethod == null)
                    {
                        throw new MethodAccessException($"Can't find compensable method {compensableMethodName} in saga type {sagaTypeInfo.FullName}");
                    }
                    if(!compensableMethod.IsPublic)
                    {
                        throw new MethodAccessException($"compensable method {compensableMethodName} in saga type {sagaTypeInfo.FullName} must be public");
                    }
                    if (compensableMethod.IsStatic)
                    {
                        throw new MethodAccessException($"compensable method {compensableMethodName} in saga type {sagaTypeInfo.FullName} can't be static");
                    }
                    if(compensableMethod.ReturnType != typeof(Task))
                    {
                        throw new MethodAccessException($"compensable method {compensableMethodName} in saga type {sagaTypeInfo.FullName} has invalid return type");
                    }
                    var compensableMethodParams = compensableMethod.GetParameters();
                    if (compensableMethodParams.Length != 1 || compensableMethodParams[0].ParameterType != typeof(FormType))
                    {
                        throw new MethodAccessException($"compensable method {compensableMethodName} in saga type {sagaTypeInfo.FullName} has invalid parameters types");
                    }
                }
            }
        }


        public abstract SagaDefinition GetSagaDefinition();

        public abstract SagaWorker GetSagaWorker();

        protected SagaDefinitionBuilder<FormType> Step()
        {
            var builder = new SagaDefinitionBuilder<FormType>();
            builder.Step();
            return builder;
        }

        // 申请新saga id
        protected async Task<string> RequestSagaId()
        {
            return await GetSagaStore().CreateSagaId<FormType>(this);
        }

        protected SagaStore GetSagaStore()
        {
            return GetSagaWorker()?.GetStore();
        }

        public async Task DoCompensationOfSaga(string sagaId, SagaData form)
        {
            await DoCompensationOfSaga(sagaId, form as FormType);
        }

        public async Task DoCompensationOfSaga(string sagaId, FormType form)
        {
            // 因为各RPC需要做到幂等性，以及需要提供空补偿，所以这里暂时直接调用补偿函数
            await GetSagaStore().CompensationStart(this, sagaId, form);
            var steps = GetSagaDefinition().Steps;
            foreach (var step in steps)
            {
                var compensation = step.Compensation;
                if (compensation != null)
                {
                    try
                    {
                        await compensation(form);
                        await GetSagaStore().CompensationDone(this, step, sagaId, form);
                        await GetSagaStore().SetSagaData(sagaId, form);
                    }
                    catch (Exception e2)
                    {
                        _logger.LogError($"DoCompensationOfSaga saga {sagaId} step {GetSagaDefinition().KeyOfStep(step)} error {e2.Message}");
                        // 补偿函数失败要重试以及记录到数据库
                        await GetSagaStore().CompensationException(this, step, sagaId, form, e2);
                        await GetSagaStore().SetSagaData(sagaId, form);
                    }
                }
                else
                {
                    await GetSagaStore().CompensationDone(this, step, sagaId, form);
                }
            }
        }

        public async Task Start(FormType form)
        {
            var sagaId = await RequestSagaId();
            await GetSagaStore().SetSagaData(sagaId, form);
            OnStarting(sagaId, form);
            var success = true;
            try
            {
                await processSteps(sagaId, form);
            }
            catch (SagaAbortException e)
            {
                success = false;
                _logger.LogError($"process saga {sagaId} error {e.Message}");
                Console.WriteLine(e.Message);
                // 补偿任务的调用异步的去处理，不需要等待处理完补偿任务再返回
                ThreadPool.QueueUserWorkItem(async (state) =>
                {
                    await DoCompensationOfSaga(sagaId, form);
                });

            }

            // process成功完成无异常则调用onSagaCompleted
            if (success)
            {
                if (!await GetSagaStore().SetSagaState(sagaId, SagaState.SUCCESS, SagaState.PROCESSING))
                {
                    // 修改状态失败，说明有其他线程修改了这个sagaId的状态为非成功状态
                    success = false;
                    OnSagaIntervalError(sagaId, form, $"");
                    return;
                }
                OnSagaCompletedSuccess(sagaId, form);
            }
        }

        public virtual void OnStarting(string sagaId, FormType sagaData)
        { }
        public virtual void OnSagaCompletedSuccess(string sagaId, FormType sagaData)
        { }

        public virtual void OnSagaIntervalError(string sagaId, FormType sagaData, string errorMsg)
        { }

        public virtual void OnSagaRolledBack(string sagaId, FormType sagaData)
        { }

        public void OnSagaRolledBack(string sagaId, SagaData sagaData)
        {
            OnSagaRolledBack(sagaId, sagaData as FormType);
        }

        public async Task processSteps(string sagaId, FormType form)
        {
            var steps = GetSagaDefinition().Steps;
            foreach (var step in steps)
            {
                try
                {
                    await step.Action(form);
                    await GetSagaStore().SetSagaData(sagaId, form);
                }
                catch (Exception e)
                {
                    if (e is SagaAbortException)
                    {
                        throw e;
                    }
                    throw new SagaAbortException(e);
                }
            }
        }
    }
}
