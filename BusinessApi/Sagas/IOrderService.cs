using commons.services.Saga;
using order_service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BusinessApi.Sagas
{
    public interface IOrderService : SagaService
    {
        [Compensable(nameof(cancelOrder))]
        Task createOrder(CreateOrderSagaData form);
        Task cancelOrder(CreateOrderSagaData form);
        Task approveOrder(CreateOrderSagaData form);

        Task<OrderDetail> queryOrder(string orderId);
        Task<ListOrdersReply> listOrders(int limit);

    }
}
