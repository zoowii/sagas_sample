using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using BusinessApi.Sagas;
using Microsoft.Extensions.Logging;
using commons.services.Saga;
using System.Threading;
using commons.services.Utils;

namespace BusinessApi.Controllers
{

    [Route("api/[controller]")]
    [ApiController]
    public class OrderController : ControllerBase
    {
        private readonly ILogger<OrderController> _logger;
        private readonly ICreateOrderSaga _createOrderSaga;
        private readonly CreateOrderSaga _realCreateOrderSaga;
        // private readonly IOrderService _orderService;
        private readonly SagaCollaborator _sagaCollaborator;
        private readonly ISagaDataConverter _sagaDataConverter;
        private readonly ISagaResolver _sagaResolver;

        public OrderController(ILogger<OrderController> logger,
            ICreateOrderSaga createOrderSaga,
            CreateOrderSaga realCreateOrderSaga,
        // IOrderService orderService,
        SagaCollaborator sagaCollaborator,
            ISagaDataConverter sagaDataConverter,
            ISagaResolver sagaResolver
            )
        {
            this._logger = logger;
            this._createOrderSaga = createOrderSaga;
            this._realCreateOrderSaga = realCreateOrderSaga;
            // this._orderService = orderService;
            this._sagaCollaborator = sagaCollaborator;
            this._sagaDataConverter = sagaDataConverter;
            this._sagaResolver = sagaResolver;
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
            await _realCreateOrderSaga.Start(form);
            if (form.RejectionReason != null)
            {
                return form.RejectionReason.ToString();
            }
            return form.OrderId;
        }

        /**
         * 这个接口是使用saga中心协作者管理动态saga steps的例子，不需要固定一个saga的步骤，中途也可以做其他业务逻辑
         */
        [HttpGet("CreateOrder2")]
        public async Task<string> CreateOrder2([FromQuery] string goodsName, [FromQuery] string customerName, [FromQuery] Int64 amount)
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

            // TODO: 把sagaContext和start/commit/rollback玻璃出来，业务代码简单写就可以了
            using (var sagaContext = new SagaContext(_sagaCollaborator,
                    _sagaDataConverter, _sagaResolver, _logger))
            {
                try
                {
                    await sagaContext.Start(form);
                    sagaContext.Bind(); // 把saga session绑定当当前async上下文中
                    // await sagaSession.InvokeAsync(_orderService.createOrder, form);
                    // await _orderService.createOrder(form);
                    // await sagaSession.InvokeAsync(_createOrderSaga.createOrder, form);
                    await _createOrderSaga.createOrder(form);
                    // await sagaSession.InvokeAsync(_createOrderSaga.reserveCustomer, form);
                    await _createOrderSaga.reserveCustomer(form);
                    // await sagaSession.InvokeAsync(_createOrderSaga.addLockedBalanceToMerchant, form);
                    await _createOrderSaga.addLockedBalanceToMerchant(form);
                    // await sagaSession.InvokeAsync(_orderService.approveOrder, form);
                    // await _orderService.approveOrder(form);
                    // await sagaSession.InvokeAsync(_createOrderSaga.approveOrder, form);
                    await _createOrderSaga.approveOrder(form);
                    // await sagaSession.InvokeAsync(_createOrderSaga.approveAddLockedBalanceToMerchant, form);
                    await _createOrderSaga.approveAddLockedBalanceToMerchant(form);
                    // await sagaSession.InvokeAsync(_createOrderSaga.addOrderHistory, form);
                    await _createOrderSaga.addOrderHistory(form);

                    await sagaContext.Commit();

                    return form.OrderId;
                }
                catch (Exception e)
                {
                    _logger.LogError("CreateOrder2 error", e);
                    await sagaContext.Rollback();
                    if (form.RejectionReason != null)
                    {
                        return form.RejectionReason.ToString();
                    }
                    return e.Message;
                }
            }
        }

    }
}
