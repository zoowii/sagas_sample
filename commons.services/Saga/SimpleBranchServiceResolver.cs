using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using commons.services.Sagas;

namespace commons.services.Saga
{
    public class SimpleBranchServiceResolver : IBranchServiceResolver
    {
        private ConcurrentDictionary<string, Func<object, Task>> _binded = new ConcurrentDictionary<string, Func<object, Task>>();

        public void Bind<T>(string serviceKey, Func<T, Task> func) where T : class, SagaData
        {
            _binded[serviceKey] = (arg) =>
            {
                return func(arg as T);
            };
        }

        public Func<object, Task> Resolve(string serviceKey)
        {
            return _binded.GetValueOrDefault(serviceKey, null);
        }

        void IBranchServiceResolver.Bind<T>(Func<T, Task> func)
        {
            var targetTypeFullName = func.Target.GetType().FullName;
            var methodName = func.Method.Name;
            var key = $"{targetTypeFullName}:{methodName}";
            Bind(key, func);
        }
    }
}
