﻿using System;
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
    public interface ISaga
    {

    }
    public interface ISimpleSaga : ISaga
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
            SagaGlobal.AutoBindBranchServices<FormType>(this, _sagaResolver, _logger);
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
