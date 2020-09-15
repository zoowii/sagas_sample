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

        public override Task<QueryOrderReply> QueryOrder(QueryOrderRequest request, ServerCallContext context)
        {
            var orderId = request.OrderId;
            var order = _orders.GetValueOrDefault(orderId);
            if(order == null)
            {
                return Task.FromResult(new QueryOrderReply()
                {
                    Success = false,
                    Message = $"can't find order {orderId}"
                });
            }
            return Task.FromResult(new QueryOrderReply()
            {
                Success = true,
                Detail = new OrderDetail()
                {
                    OrderId = order.OrderId,
                    CustomerName = order.CustomerName,
                    GoodsName = order.GoodsName,
                    Amount = order.Amount,
                    State = Convert.ToInt32(order.State)
                }
            });
        }

        public override Task<ListOrdersReply> ListOrders(ListOrdersRequest request, ServerCallContext context)
        {
            var orderIds = _orders.Keys;
            var list = new List<OrderDetail>();
            foreach(var orderId in orderIds)
            {
                var order = _orders[orderId];
                var item = new OrderDetail()
                {
                    OrderId = order.OrderId,
                    CustomerName = order.CustomerName,
                    GoodsName = order.GoodsName,
                    Amount = order.Amount,
                    State = Convert.ToInt32(order.State)
                };
                list.Add(item);
            }
            var reply = new ListOrdersReply()
            {
                Success = true,
                Total = list.Count()
            };
            reply.Details.AddRange(list);
            return Task.FromResult(reply);
        }
    }
}
