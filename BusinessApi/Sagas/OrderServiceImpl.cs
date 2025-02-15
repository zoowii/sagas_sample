﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BusinessApi.Exceptions;
using commons.services.Saga;
using commons.services.Sagas;
using order_service;

namespace BusinessApi.Sagas
{
    //  定义IOrderService和OrderServiceImpl, startup时自动产生实现了IOrderService的代理类，
    //  代理类在调用实际方法前会调用currentSagaSession.InvokeAsync
    public class OrderServiceImpl : IOrderService
    {
        private readonly GrpcClientsHolder _grpcClientsHolder;

        public OrderServiceImpl(GrpcClientsHolder grpcClientsHolder)
        {
            this._grpcClientsHolder = grpcClientsHolder;
        }

        [Compensable(nameof(cancelOrder))]
        public async Task createOrder(CreateOrderSagaData form)
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
        public async Task cancelOrder(CreateOrderSagaData form)
        {
            try
            {
                if (form.OrderId == null)
                {
                    return;
                }
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


        public async Task approveOrder(CreateOrderSagaData form)
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

        public async Task<OrderDetail> queryOrder(string orderId)
        {
            var reply = await _grpcClientsHolder.OrderClient.QueryOrderAsync(
                new QueryOrderRequest
                { 
                    OrderId = orderId 
                });
            if(!reply.Success)
            {
                throw new ServerErrorException(reply.Message);
            }
            return reply.Detail;
        }

        public async Task<ListOrdersReply> listOrders(int limit)
        {
            var reply = await _grpcClientsHolder.OrderClient.ListOrdersAsync(
                new ListOrdersRequest
                {
                    Limit = limit
                });
            if (!reply.Success)
            {
                throw new ServerErrorException(reply.Message);
            }
            return reply;
        }
    }
}
