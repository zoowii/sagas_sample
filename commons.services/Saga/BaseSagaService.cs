using commons.services.Sagas;
using commons.services.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace commons.services.Saga
{
    public class BaseSagaService<T> : SagaService where T: class, SagaData
    {
        // SagaService的实现类的代理类要在业务方法中拦截字节码增强为调用OnSagaCall(被代理对象的func, arg)
        public async Task OnSagaCall(SagaSession<T>.StepCallback func, T sagaData)
        {
            if(this.GetType() == typeof(BaseSagaService<T>))
            {
                throw new NotImplementedException();
            }
            // get current sagaSession and call invoke
            SagaSession<T> sagaSession = CallContext.GetData(SagaGlobal.SAGA_SESSION_CONTEXT_KEY) as SagaSession<T>;
            if(sagaSession == null)
            {
                throw new InvalidOperationException($"saga session not fetched");
            }
            await sagaSession.InvokeAsync(func, sagaData);
        }
    }
}
