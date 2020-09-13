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
        // TODO: SagaService的实现类的代理类要在业务方法中拦截字节码增强为调用OnSagaCall(被代理对象的func, arg)
        public async Task OnSagaCall(SagaContext<T>.StepCallback func, T sagaData)
        {
            if(this.GetType() == typeof(BaseSagaService<T>))
            {
                throw new NotImplementedException();
            }
            // get current sagaContext and call invoke
            SagaContext<T> sagaContext = CallContext.GetData(SagaGlobal.SAGA_CONTEXT_CONTEXT_KEY) as SagaContext<T>;
            if(sagaContext == null)
            {
                throw new InvalidOperationException($"saga context not fetched");
            }
            await sagaContext.InvokeAsync(func, sagaData);
        }
    }
}
