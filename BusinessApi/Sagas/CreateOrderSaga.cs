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
using commons.services.Saga;

namespace BusinessApi.Sagas
{
    public class CreateOrderSaga : SimpleSaga<CreateOrderSagaData>, ICreateOrderSaga
    {
        // private readonly SagaWorker _sagaWorker;
        private readonly SagaDefinition sagaDefinition;

        private readonly GrpcClientsHolder _grpcClientsHolder;
        private readonly OrderServiceImpl _orderService;
        private readonly ISagaResolver _sagaResolver;

        private readonly ILogger<CreateOrderSaga> _logger;


        public CreateOrderSaga(
            /* SagaWorker sagaWorker, */
            GrpcClientsHolder grpcClientsHolder,
            /* 这里注入orderService真实业务对象而不是saga代理对象，是为了避免两次sagaSession.invokeAsync重复注册一段代码为2各branchId */
            OrderServiceImpl orderService,
            ISagaResolver sagaResolver,
            ILogger<CreateOrderSaga> logger)
            : base(logger, sagaResolver)
        {
            // this._sagaWorker = sagaWorker;
            this._grpcClientsHolder = grpcClientsHolder;
            this._orderService = orderService;
            this._sagaResolver = sagaResolver;
            this._logger = logger;


            // 本saga中服务会自动注入reslver，额外的服务需要手动注入
            //_sagaResolver.BindBranch<CreateOrderSagaData>( _orderService.createOrder);
            //_sagaResolver.BindBranch<CreateOrderSagaData>(_orderService.cancelOrder);
            //_sagaResolver.BindBranch<CreateOrderSagaData>(_orderService.approveOrder);
            

            // 显示定义 sagaDefinition是可选的，如果使用saga server管理动态分支，则不需要显示定义sagaDefinition
            sagaDefinition = Step()
                //.SetRemoteAction(createOrder)
                //.WithCompensation(cancelOrder)
                .SetRemoteAction(createOrder)
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
            return null;
            // return _sagaWorker;
        }

        [Compensable(nameof(cancelOrder))]
        public async Task createOrder(CreateOrderSagaData form)
        {
            await _orderService.createOrder(form);
        }

        public async Task cancelOrder(CreateOrderSagaData form)
        {
            await _orderService.cancelOrder(form);
        }

        public async Task approveOrder(CreateOrderSagaData form)
        {
            await _orderService.approveOrder(form);
        }

        // 用public而不是private是为了暴露给sagaSession调用
        [Compensable(nameof(cancelReserveCustomer))]
        public async Task reserveCustomer(CreateOrderSagaData form)
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

        // public to sagaSession
        public async Task cancelReserveCustomer(CreateOrderSagaData form)
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

        // public只暴露给sagaSession
        [Compensable(nameof(cancelAddLockedBalanceToMerchant))]
        public async Task addLockedBalanceToMerchant(CreateOrderSagaData form)
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

        // public to sagaSession
        public async Task cancelAddLockedBalanceToMerchant(CreateOrderSagaData form)
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

        // public to SagaSession
        public async Task approveAddLockedBalanceToMerchant(CreateOrderSagaData form)
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

        // public to sagaSession
        [Compensable(nameof(cancelOrderHistory))]
        public async Task addOrderHistory(CreateOrderSagaData form)
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

        // public to sagaSession
        public async Task cancelOrderHistory(CreateOrderSagaData form)
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
