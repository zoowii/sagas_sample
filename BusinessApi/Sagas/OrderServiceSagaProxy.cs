using commons.services.Saga;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BusinessApi.Sagas
{
    public class OrderServiceSagaProxy : BaseSagaService<CreateOrderSagaData>, IOrderService
    {
        private readonly IOrderService _decorated;
        public OrderServiceSagaProxy(OrderServiceImpl decroated)
        {
            this._decorated = decroated;
        }

        public async Task approveOrder(CreateOrderSagaData form)
        {
            await OnSagaCall(_decorated.approveOrder, form);
        }

        public async Task cancelOrder(CreateOrderSagaData form)
        {
            await OnSagaCall(_decorated.cancelOrder, form);
        }

        public async Task createOrder(CreateOrderSagaData form)
        {
            await OnSagaCall(_decorated.createOrder, form);
        }
    }
}
