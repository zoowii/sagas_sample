using commons.services.Sagas;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace commons.services.Saga
{
    public sealed class SagaGlobal
    {
        public const string SAGA_XID_CONTEXT_KEY = "saga_xid"; // saga xid在CallContext中的上下文变量
        public const string SAGA_SESSION_CONTEXT_KEY = "saga_session"; // sagaSession在CallContext中的上下文变量


        private static ConcurrentDictionary<string, Type> _bindedSagaDataTypes = new ConcurrentDictionary<string, Type>();

        public static void BindSagaDataType(Type sagaDataType)
        {
            var fullTypeName = sagaDataType.FullName;
            _bindedSagaDataTypes[fullTypeName] = sagaDataType;
        }

        public static Type ResolveSagaDataType(string fullTypeName)
        {
            return _bindedSagaDataTypes.GetValueOrDefault(fullTypeName, null);
        }


        // saga的实现类的各满足要求的方法，以及compensable方法（如果还没注册过的话）自动BindBranch，
        // 这样避免具体实现类中手动写
        public static void AutoBindBranchServices<FormType>(
            object instance,
            ISagaResolver sagaResolver,
            ILogger logger
            ) where FormType : class, SagaData
        {
            var sagaTypeInfo = instance.GetType();
            var methods = sagaTypeInfo.GetMethods();
            foreach (var method in methods)
            {
                if (!method.IsPublic)
                {
                    continue;
                }
                if (method.IsStatic)
                {
                    continue;
                }
                if (method.ReturnType != typeof(Task))
                {
                    continue;
                }
                var methodParams = method.GetParameters();
                if (methodParams.Length != 1 || methodParams[0].ParameterType != typeof(FormType))
                {
                    continue;
                }
                Func<FormType, Task> func = delegate (FormType form)
                {
                    return method.Invoke(instance, new object[] { form }) as Task;
                };
                // var action = DelegateBuilder.BuildDelegate<Func<FormType, Task>>(method, sagaTypeInfo);
                var serviceKey = sagaResolver.GetServiceKey(sagaTypeInfo, method.Name);
                sagaResolver.BindBranch<FormType>(serviceKey, func);
                logger.LogInformation($"saga resolver binded action {func}");
                // 如果有compensable注解，要求方法在本类中，并且是public方法
                var compensableAttr = Utils.MethodUtils.GetDeclaredAttribute<Compensable>(method, typeof(Compensable));
                if (compensableAttr != null)
                {
                    var compensableMethodName = compensableAttr.ActionName;
                    var compensableMethod = Utils.MethodUtils.GetMethod(sagaTypeInfo, compensableMethodName);
                    if (compensableMethod == null)
                    {
                        throw new MethodAccessException($"Can't find compensable method {compensableMethodName} in saga type {sagaTypeInfo.FullName}");
                    }
                    if (!compensableMethod.IsPublic)
                    {
                        throw new MethodAccessException($"compensable method {compensableMethodName} in saga type {sagaTypeInfo.FullName} must be public");
                    }
                    if (compensableMethod.IsStatic)
                    {
                        throw new MethodAccessException($"compensable method {compensableMethodName} in saga type {sagaTypeInfo.FullName} can't be static");
                    }
                    if (compensableMethod.ReturnType != typeof(Task))
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
    }
}
