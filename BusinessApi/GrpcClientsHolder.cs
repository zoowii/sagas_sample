using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using customer_service;
using order_service;
using merchant_service;
using history_service;
using saga_server;
using commons.services.Saga;


namespace BusinessApi
{
    public class GrpcClientsHolder
    {
        public Order.OrderClient OrderClient { get; set; }
        public Customer.CustomerClient CustomerClient { get; set; }

        public Merchant.MerchantClient MerchantClient { get; set; }

        public History.HistoryClient HistoryClient { get; set; }

        public SagaServer.SagaServerClient SagaServerClient { get; set; }

    }
}
