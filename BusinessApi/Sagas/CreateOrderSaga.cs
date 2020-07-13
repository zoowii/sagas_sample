using Grpc.Net.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using apis.Exceptions;
using commons.services.Sagas;
using Microsoft.Extensions.Logging;
using customer_service;
using order_service;
using merchant_service;
using history_service;

namespace BusinessApi.Sagas
{
    // TODO: 分布式的saga协调者.暂时在网关发起，独立跑集中式协调者
    // TODO: 每个saga service的业务方法和补偿方法开头都向saga server请求xid和step id，记录本step到本sagaId中，从而可以有动态saga规则定义，而不是需要预定义saga definition

    public class CreateOrderSaga : SimpleSaga<CreateOrderSagaData>
    {
        private readonly SagaWorker sagaWorker;
        private readonly SagaDefinition sagaDefinition;

        private readonly GrpcClientsHolder _grpcClientsHolder;
        private readonly OrderService _orderService;

        private readonly ILogger<CreateOrderSaga> _logger;

        public CreateOrderSaga(SagaWorker sagaWorker, GrpcClientsHolder grpcClientsHolder,
            OrderService orderService,
            ILogger<CreateOrderSaga> logger)
            : base(logger)
        {
            this.sagaWorker = sagaWorker;
            this._grpcClientsHolder = grpcClientsHolder;
            this._orderService = orderService;
            this._logger = logger;

            sagaDefinition = Step()
                //.SetRemoteAction(createOrder)
                //.WithCompensation(cancelOrder)
                .SetRemoteAction(_orderService.createOrder)
                .Step()
                .SetRemoteAction(reserveCustomer)
                .WithCompensation(cancelReserveCustomer)
                .Step()
                .SetRemoteAction(addLockedBalanceToMerchant)
                .WithCompensation(cancelAddLockedBalanceToMerchant)
                .Step()
                //.SetRemoteAction(approveOrder)
                .SetRemoteAction(_orderService.approveOrder)
                .Step()
                .SetRemoteAction(approveAddLockedBalanceToMerchant)
                .Step()
                .SetRemoteAction(addOrderHistory)
                .WithCompensation(cancelOrderHistory)
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
                if (form.OrderId == null)
                {
                    return;
                }
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
                if(form.OrderId==null)
                {
                    return;
                }
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
                if (form.OrderId == null)
                {
                    return;
                }
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

        private async Task addOrderHistory(CreateOrderSagaData form)
        {
            try
            {
                if (form.OrderId == null)
                {
                    return;
                }
                var reply = await _grpcClientsHolder.HistoryClient.AddOrderHistoryAsync(
                    new AddOrderHistoryRequest
                    {
                        OrderId = form.OrderId,
                        Amount = form.CreateOrder.Amount,
                        CustomerName = form.CreateOrder.CustomerName
                    });
                if (!reply.Success)
                {
                    form.RejectionReason = CreateOrderRejectionReason.UNKNOWN_ERROR;
                    throw new SagaAbortException($"addOrderHistory failed {reply.Message}");
                }
            }
            catch (Exception e)
            {
                form.RejectionReason = CreateOrderRejectionReason.UNKNOWN_ERROR;
                throw new SagaAbortException(e);
            }
        }

        private async Task cancelOrderHistory(CreateOrderSagaData form)
        {
            try
            {
                if (form.OrderId == null)
                {
                    return;
                }
                var reply = await _grpcClientsHolder.HistoryClient.CancelOrderHistoryAsync(
                    new CancelOrderHistoryRequest
                    {
                        OrderId = form.OrderId,
                        Amount = form.CreateOrder.Amount,
                        CustomerName = form.CreateOrder.CustomerName,
                        Reason = form.RejectionReason.ToString()
                    });
                if (!reply.Success)
                {
                    form.RejectionReason = CreateOrderRejectionReason.UNKNOWN_ERROR;
                    throw new SagaAbortException($"cancelOrderHistory failed {reply.Message}");
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
