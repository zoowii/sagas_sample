using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace order_service.Services
{
    public enum OrderState
    {
        PENDING,
        SUBMITED,
        ABORTED
    }
    public class OrderEntity
    {
        public string OrderId { get; set; }
        public string CustomerName { get; set; }
        public string GoodsName { get; set; }
        public Int64 Amount { get; set; }
        public OrderState State { get; set; }
    }
}
