using commons.services.Sagas;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace commons.services.Saga
{
    /*
     * saga的resolver和注册绑定服务/配置的接口
     * 用来根据service key找到分支事务的service method的方法
     */
    public interface ISagaResolver
    {

        void BindBranch<T>(string serviceKey, Func<T, Task> func) where T : class, SagaData;

        void BindBranch<T>(Func<T, Task> func) where T : class, SagaData;
        Func<object, Task> ResolveBranch(string serviceKey);

        string GetServiceKey(Type typeInfo, string methodName);
    }
}
