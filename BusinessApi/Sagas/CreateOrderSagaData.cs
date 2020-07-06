using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using order_service;
using commons.services.Sagas;

namespace BusinessApi.Sagas
{
    public class CreateOrderSagaData : SagaData
    {
        public CreateOrderRequest CreateOrder { get; set; }
        public string OrderId { get; set; }
        public string MerchantName { get; set; }

        public CreateOrderRejectionReason? RejectionReason { get; set; }
    }
}
