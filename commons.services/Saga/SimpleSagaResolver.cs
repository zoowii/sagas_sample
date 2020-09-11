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

        public void BindBranch<T>(string serviceKey, Func<T, Task> func) where T : class, SagaData
        {
            _bindedBranchServices[serviceKey] = (arg) =>
            {
                return func(arg as T);
            };
        }

        public string GetServiceKey(Type typeInfo, string methodName)
        {
            return $"{typeInfo.FullName}:{methodName}";
        }

        public Func<object, Task> ResolveBranch(string serviceKey)
        {
            return _bindedBranchServices.GetValueOrDefault(serviceKey, null);
        }

        void ISagaResolver.BindBranch<T>(Func<T, Task> func)
        {
            var methodName = func.Method.Name;
            var key = GetServiceKey(func.Target.GetType(), methodName);
            BindBranch(key, func);
        }
    }
}
