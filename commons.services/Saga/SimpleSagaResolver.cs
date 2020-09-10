using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using commons.services.Sagas;

namespace commons.services.Saga
{
    public class SimpleSagaResolver : ISagaResolver
    {
        private ConcurrentDictionary<string, Func<object, Task>> _bindedBranchServices = new ConcurrentDictionary<string, Func<object, Task>>();
        private ConcurrentDictionary<string, Type> _bindedSagaDataTypes = new ConcurrentDictionary<string, Type>();

        public void BindBranch<T>(string serviceKey, Func<T, Task> func) where T : class, SagaData
        {
            _bindedBranchServices[serviceKey] = (arg) =>
            {
                return func(arg as T);
            };
        }

        public Func<object, Task> ResolveBranch(string serviceKey)
        {
            return _bindedBranchServices.GetValueOrDefault(serviceKey, null);
        }

        void ISagaResolver.BindBranch<T>(Func<T, Task> func)
        {
            var targetTypeFullName = func.Target.GetType().FullName;
            var methodName = func.Method.Name;
            var key = $"{targetTypeFullName}:{methodName}";
            BindBranch(key, func);
        }

        public void BindSagaDataType(Type sagaDataType)
        {
            var fullTypeName = sagaDataType.FullName;
            _bindedSagaDataTypes[fullTypeName] = sagaDataType;
        }

        public Type ResolveSagaDataType(string fullTypeName)
        {
            return _bindedSagaDataTypes.GetValueOrDefault(fullTypeName, null);
        }
    }
}
