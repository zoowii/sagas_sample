using commons.services.Sagas;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace commons.services.Saga
{
    /*
     * 用来根据service key找到分支事务的service method的方法
     */
    public interface IBranchServiceResolver
    {

        void Bind<T>(string serviceKey, Func<T, Task> func) where T : class, SagaData;

        void Bind<T>(Func<T, Task> func) where T : class, SagaData;
        Func<object, Task> Resolve(string serviceKey);
    }
}
