using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using apis.Exceptions;

namespace customer_service
{
    public class CustomerService : Customer.CustomerBase
    {
        private readonly ILogger<CustomerService> _logger;
        private Dictionary<string, Int64> _customerReservedDebts;
        private Dictionary<string, Int64> _appliedOrderAmounts;
        public CustomerService(ILogger<CustomerService> logger)
        {
            _logger = logger;
            _customerReservedDebts = new Dictionary<string, long>();
            _appliedOrderAmounts = new Dictionary<string, Int64>();
        }

        public override Task<ReserveCreditReply> ReserveCredit(ReserveCreditRequest request, ServerCallContext context)
        {
            _logger.LogInformation($"reserve customer credit {request.ToString()}");

            if (request.Amount > 1 || request.Amount < 0)
            {
                throw new CustomerException("amount only accept 1 now");
            }
            if(!_customerReservedDebts.ContainsKey(request.CustomerName))
            {
                _customerReservedDebts[request.CustomerName] = request.Amount;
            }
            else
            {
                _customerReservedDebts[request.CustomerName] += request.Amount;
            }
            _appliedOrderAmounts[request.OrderId] = request.Amount;
            return Task.FromResult(new ReserveCreditReply
            {
                Success = true,
                Message = "customer reserved credit for order " + request.OrderId
            });
        }

        public override Task<CancelReserveCreditReply> CancelReserveCredit(CancelReserveCreditRequest request, ServerCallContext context)
        {
            _logger.LogInformation($"cancel reserve customer credit {request.ToString()}");
            if(_appliedOrderAmounts.ContainsKey(request.OrderId))
            {
                _customerReservedDebts[request.CustomerName] -= _appliedOrderAmounts[request.OrderId];
                _appliedOrderAmounts.Remove(request.OrderId);
            }
            // TODO
            return Task.FromResult(new CancelReserveCreditReply
            {
                Success = true,
                Message = "canceled reserved credit for order " + request.OrderId
            });
        }
    }
}
