using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using customer_service;
using order_service;
using merchant_service;


namespace BusinessApi
{
    public class GrpcClientsHolder
    {
        public Order.OrderClient OrderClient { get; set; }
        public Customer.CustomerClient CustomerClient { get; set; }

        public Merchant.MerchantClient MerchantClient { get; set; }
    }
}
