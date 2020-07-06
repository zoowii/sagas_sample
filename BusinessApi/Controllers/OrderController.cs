using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using BusinessApi.Sagas;

namespace BusinessApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrderController : ControllerBase
    {
        private readonly CreateOrderSaga _createOrderSaga;

        public OrderController(CreateOrderSaga createOrderSaga)
        {
            this._createOrderSaga = createOrderSaga;
        }

        // GET: api/Order
        [HttpGet]
        public IEnumerable<string> Get()
        {
            // TODO: 查询order列表
            return new string[] { "value1", "value2" };
        }

        // GET: api/Order/5
        [HttpGet("{id}", Name = "Get")]
        public string Get(int id)
        {
            // TODO: 查询某个order
            return "value";
        }

        // example url: http://localhost:65263/api/order/createOrder?goodsName=pen&customerName=zhang3&amount=1
        // GET: api/CreateOrder
        [HttpGet("CreateOrder")]
        public async Task<string> CreateOrder([FromQuery] string goodsName, [FromQuery] string customerName, [FromQuery] Int64 amount)
        {
            var form = new CreateOrderSagaData
            {
                CreateOrder = new order_service.CreateOrderRequest
                {
                    CustomerName = customerName,
                    GoodsName = goodsName,
                    Amount = amount
                }
            };
            await _createOrderSaga.Start(form);
            if(form.RejectionReason != null)
            {
                return form.RejectionReason.ToString();
            }
            return form.OrderId;
        }

    }
}
