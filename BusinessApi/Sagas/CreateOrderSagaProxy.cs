using commons.services.Saga;
using commons.services.Sagas;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BusinessApi.Sagas
{
    public class CreateOrderSagaProxy : BaseSagaService<CreateOrderSagaData>, ICreateOrderSaga
    {
        private readonly CreateOrderSaga _decorated;
        public CreateOrderSagaProxy(CreateOrderSaga decorated)
        {
            this._decorated = decorated;
        }

        public async Task addLockedBalanceToMerchant(CreateOrderSagaData form)
        {
            await OnSagaCall(_decorated.addLockedBalanceToMerchant, form);
        }

        public async Task addOrderHistory(CreateOrderSagaData form)
        {
            await OnSagaCall(_decorated.addOrderHistory, form);
        }

        public async Task approveAddLockedBalanceToMerchant(CreateOrderSagaData form)
        {
            await OnSagaCall(_decorated.approveAddLockedBalanceToMerchant, form);
        }

        public async Task approveOrder(CreateOrderSagaData form)
        {
            await OnSagaCall(_decorated.approveOrder, form);
        }

        public async Task cancelAddLockedBalanceToMerchant(CreateOrderSagaData form)
        {
            await OnSagaCall(_decorated.cancelAddLockedBalanceToMerchant, form);
        }

        public async Task cancelOrder(CreateOrderSagaData form)
        {
            await OnSagaCall(_decorated.cancelOrder, form);
        }

        public async Task cancelOrderHistory(CreateOrderSagaData form)
        {
            await OnSagaCall(_decorated.cancelOrderHistory, form);
        }

        public async Task cancelReserveCustomer(CreateOrderSagaData form)
        {
            await OnSagaCall(_decorated.cancelReserveCustomer, form);
        }

        public async Task createOrder(CreateOrderSagaData form)
        {
            await OnSagaCall(_decorated.createOrder, form);
        }

        public Task DoCompensationOfSaga(string sagaId, SagaData form)
        {
            return _decorated.DoCompensationOfSaga(sagaId, form);
        }

        public SagaDefinition GetSagaDefinition()
        {
            return _decorated.GetSagaDefinition();
        }

        public SagaWorker GetSagaWorker()
        {
            return _decorated.GetSagaWorker();
        }

        public void OnSagaRolledBack(string sagaId, SagaData sagaData)
        {
            _decorated.OnSagaRolledBack(sagaId, sagaData);
        }

        public async Task reserveCustomer(CreateOrderSagaData form)
        {
            await OnSagaCall(_decorated.reserveCustomer, form);
        }
    }
}
