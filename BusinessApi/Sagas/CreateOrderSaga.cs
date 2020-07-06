using Grpc.Net.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using customer_service;
using order_service;
using merchant_service;
using apis.Exceptions;
using commons.services.Sagas;
using Microsoft.Extensions.Logging;

namespace BusinessApi.Sagas
{
    // TODO: 需要可以给客户端调用saga并像调用saga的第一个步骤一样处理，或者可以选择要等整个saga完成再返回
    // TODO: 其他语言提供grpc服务
    // TODO: 分布式的saga协调者.暂时在网关发起，独立跑集中式协调者

    public class CreateOrderSaga : SimpleSaga<CreateOrderSagaData>
    {
        private readonly SagaWorker sagaWorker;
        private readonly SagaDefinition sagaDefinition;

        private GrpcClientsHolder _grpcClientsHolder;

        private readonly ILogger<CreateOrderSaga> _logger;

        public CreateOrderSaga(SagaWorker sagaWorker, GrpcClientsHolder grpcClientsHolder, ILogger<CreateOrderSaga> logger)
            : base(logger)
        {
            this.sagaWorker = sagaWorker;
            this._grpcClientsHolder = grpcClientsHolder;
            this._logger = logger;

            sagaDefinition = Step()
                .SetRemoteAction(createOrder)
                .WithCompensation(cancelOrder)
                .Step()
                .SetRemoteAction(reserveCustomer)
                .WithCompensation(cancelReserveCustomer)
                .Step()
                .SetRemoteAction(addLockedBalanceToMerchant)
                .WithCompensation(cancelAddLockedBalanceToMerchant)
                .Step()
                .SetRemoteAction(approveOrder)
                .Step()
                .SetRemoteAction(approveAddLockedBalanceToMerchant)
                .Build(this);
        }

        public override SagaDefinition GetSagaDefinition()
        {
            return sagaDefinition;
        }

        public override SagaWorker GetSagaWorker()
        {
            return sagaWorker;
        }

        private async Task createOrder(CreateOrderSagaData form)
        {
            try
            {
                var reply = await _grpcClientsHolder.OrderClient.CreateOrderAsync(form.CreateOrder);
                if (!reply.Success)
                {
                    form.RejectionReason = CreateOrderRejectionReason.UNKNOWN_ERROR;
                    throw new SagaAbortException($"create credit failed {reply.Message}");
                }
                form.OrderId = reply.OrderId;
            }
            catch (Exception e)
            {
                form.RejectionReason = CreateOrderRejectionReason.UNKNOWN_ERROR;
                throw new SagaAbortException(e);
            }
        }
        private async Task cancelOrder(CreateOrderSagaData form)
        {
            try
            {
                var reply = await _grpcClientsHolder.OrderClient.CancelOrderAsync(
                    new CancelOrderRequest
                    {
                        OrderId = form.OrderId
                    });
                if (!reply.Success)
                {
                    form.RejectionReason = CreateOrderRejectionReason.UNKNOWN_ERROR;
                    throw new SagaAbortException($"create credit failed {reply.Message}");
                }
            }
            catch (Exception e)
            {
                form.RejectionReason = CreateOrderRejectionReason.UNKNOWN_ERROR;
                throw new SagaAbortException(e);
            }
        }

        private async Task reserveCustomer(CreateOrderSagaData form)
        {
            try
            {
                var reply = await _grpcClientsHolder.CustomerClient.ReserveCreditAsync(
                    new ReserveCreditRequest
                    {
                        OrderId = form.OrderId,
                        CustomerName = form.CreateOrder.CustomerName,
                        Amount = form.CreateOrder.Amount
                    });
                if (!reply.Success)
                {
                    form.RejectionReason = CreateOrderRejectionReason.INSUFFICIENT_CREDIT;
                    throw new SagaAbortException($"customer reserve credit failed {reply.Message}");
                }
            }
            catch (Exception e)
            {
                form.RejectionReason = CreateOrderRejectionReason.INSUFFICIENT_CREDIT;
                throw new SagaAbortException(e);
            }
        }

        private async Task cancelReserveCustomer(CreateOrderSagaData form)
        {
            try
            {
                var reply = await _grpcClientsHolder.CustomerClient.CancelReserveCreditAsync(
                     new CancelReserveCreditRequest
                     {
                         OrderId = form.OrderId,
                         CustomerName = form.CreateOrder.CustomerName
                     });
                if (!reply.Success)
                {
                    form.RejectionReason = CreateOrderRejectionReason.UNKNOWN_ERROR;
                    throw new SagaAbortException($"customer cancel reserve credit failed {reply.Message}");
                }
            }
            catch (Exception e)
            {
                form.RejectionReason = CreateOrderRejectionReason.UNKNOWN_ERROR;
                throw new SagaAbortException(e);
            }
        }

        private async Task approveOrder(CreateOrderSagaData form)
        {
            try
            {
                var reply = await _grpcClientsHolder.OrderClient.ApproveAsync(
                    new ApproveRequest
                    {
                        OrderId = form.OrderId
                    });
                if (!reply.Success)
                {
                    form.RejectionReason = CreateOrderRejectionReason.UNKNOWN_ERROR;
                    throw new SagaAbortException($"approve order failed {reply.Message}");
                }
            }
            catch (Exception e)
            {
                form.RejectionReason = CreateOrderRejectionReason.UNKNOWN_ERROR;
                throw new SagaAbortException(e);
            }
        }

        private async Task addLockedBalanceToMerchant(CreateOrderSagaData form)
        {
            try
            {
                var reply = await _grpcClientsHolder.MerchantClient.AddLockedBalanceAsync(
                    new AddLockedBalanceRequest
                    {
                        OrderId = form.OrderId,
                        Amount = form.CreateOrder.Amount,
                        MerchantName = form.CreateOrder.CustomerName // 把商品名称当作店家名
                    });
                if (!reply.Success)
                {
                    form.RejectionReason = CreateOrderRejectionReason.UNKNOWN_ERROR;
                    throw new SagaAbortException($"addLockedBalanceToMerchant failed {reply.Message}");
                }
            }
            catch(Exception e)
            {
                form.RejectionReason = CreateOrderRejectionReason.UNKNOWN_ERROR;
                throw new SagaAbortException(e);
            }
        }

        private async Task cancelAddLockedBalanceToMerchant(CreateOrderSagaData form)
        {
            try
            {
                var reply = await _grpcClientsHolder.MerchantClient.CancelAddLockedBalanceAsync(
                    new CancelAddLockedBalanceRequest
                    {
                        OrderId = form.OrderId,
                        Amount = form.CreateOrder.Amount,
                        MerchantName = form.CreateOrder.CustomerName // 把商品名称当作店家名
                    });
                if (!reply.Success)
                {
                    form.RejectionReason = CreateOrderRejectionReason.UNKNOWN_ERROR;
                    throw new SagaAbortException($"cancelAddLockedBalanceToMerchant failed {reply.Message}");
                }
            }
            catch (Exception e)
            {
                form.RejectionReason = CreateOrderRejectionReason.UNKNOWN_ERROR;
                throw new SagaAbortException(e);
            }
        }

        private async Task approveAddLockedBalanceToMerchant(CreateOrderSagaData form)
        {
            try
            {
                var reply = await _grpcClientsHolder.MerchantClient.ApproveLockedBalanceAsync(
                    new ApproveLockedBalanceRequest
                    {
                        OrderId = form.OrderId,
                        Amount = form.CreateOrder.Amount,
                        MerchantName = form.CreateOrder.CustomerName // 把商品名称当作店家名
                    });
                if (!reply.Success)
                {
                    form.RejectionReason = CreateOrderRejectionReason.UNKNOWN_ERROR;
                    throw new SagaAbortException($"addLockedBalanceToMerchant failed {reply.Message}");
                }
            }
            catch (Exception e)
            {
                form.RejectionReason = CreateOrderRejectionReason.UNKNOWN_ERROR;
                throw new SagaAbortException(e);
            }
        }

    }
}
