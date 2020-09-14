using commons.services.Saga;
using commons.services.Sagas;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BusinessApi.Sagas
{
    public interface ICreateOrderSaga : ISimpleSaga
    {

        [Compensable(nameof(cancelOrder))]
        Task createOrder(CreateOrderSagaData form);
        Task cancelOrder(CreateOrderSagaData form);

        Task approveOrder(CreateOrderSagaData form);
        [Compensable(nameof(cancelReserveCustomer))]
        Task reserveCustomer(CreateOrderSagaData form);
        Task cancelReserveCustomer(CreateOrderSagaData form);
        [Compensable(nameof(cancelAddLockedBalanceToMerchant))]
        Task addLockedBalanceToMerchant(CreateOrderSagaData form);
        Task cancelAddLockedBalanceToMerchant(CreateOrderSagaData form);
        Task approveAddLockedBalanceToMerchant(CreateOrderSagaData form);
        [Compensable(nameof(cancelOrderHistory))]
        Task addOrderHistory(CreateOrderSagaData form);
        Task cancelOrderHistory(CreateOrderSagaData form);
    }
}
