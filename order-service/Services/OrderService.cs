using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using order_service.Services;

namespace order_service
{
    public class OrderService : Order.OrderBase
    {
        private readonly ILogger<OrderService> _logger;

        private Dictionary<string, OrderEntity> _orders;
        public OrderService(ILogger<OrderService> logger)
        {
            _logger = logger;
            _orders = new Dictionary<string, OrderEntity>();
        }


        public override Task<CreateOrderReply> CreateOrder(CreateOrderRequest request, ServerCallContext context)
        {
            _logger.LogInformation($"create order {request.ToString()} as pending state");
            var orderId = Guid.NewGuid().ToString();
            var orderEntity = new OrderEntity
            {
                OrderId = orderId,
                CustomerName = request.CustomerName,
                GoodsName = request.GoodsName,
                Amount = request.Amount,
                State = OrderState.PENDING
            };
            _orders[orderId] = orderEntity;
            return Task.FromResult(new CreateOrderReply
            {
                Success = true,
                Message = "Hello " + request.CustomerName,
                OrderId = orderId
            });
        }

        public override Task<CancelOrderReply> CancelOrder(CancelOrderRequest request, ServerCallContext context)
        {
            var orderId = request.OrderId;
            _logger.LogInformation($"canceling order {orderId}");
            if(_orders.ContainsKey(orderId))
            {
                _orders[orderId].State = OrderState.ABORTED;
            }
            return Task.FromResult(new CancelOrderReply
            {
                Success = true,
                Message = $"canceled order {orderId}"
            });
        }

        public override Task<ApproveReply> Approve(ApproveRequest request, ServerCallContext context)
        {
            var orderId = request.OrderId;
            _logger.LogInformation($"approving order {orderId}");
            if(_orders.ContainsKey(orderId))
            {
                var order = _orders[orderId];
                order.State = OrderState.SUBMITED;
            }
            return Task.FromResult(new ApproveReply
            {
                Success = true,
                Message = $"approved order {orderId}"
            });
        }
    }
}
