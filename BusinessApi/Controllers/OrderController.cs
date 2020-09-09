using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using BusinessApi.Sagas;
using Microsoft.Extensions.Logging;
using commons.services.Saga;

namespace BusinessApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrderController : ControllerBase
    {
        private readonly ILogger<OrderController> _logger;
        private readonly CreateOrderSaga _createOrderSaga;
        private readonly GrpcClientsHolder _grpcClientsHolder;
        private readonly OrderService _orderService;
        private readonly SagaCollaborator _sagaCollaborator;
        private readonly ISagaDataConverter _sagaDataConverter;

        public OrderController(ILogger<OrderController> logger, CreateOrderSaga createOrderSaga,
            GrpcClientsHolder grpcClientsHolder,
            OrderService orderService,
            SagaCollaborator sagaCollaborator,
            ISagaDataConverter sagaDataConverter
            )
        {
            this._logger = logger;
            this._createOrderSaga = createOrderSaga;
            this._grpcClientsHolder = grpcClientsHolder;
            this._orderService = orderService;
            this._sagaCollaborator = sagaCollaborator;
            this._sagaDataConverter = sagaDataConverter;
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
            // TODO: 这个改成中心saga协调者方式注册xid和branch tx的方式


            var form = new CreateOrderSagaData
            {
                CreateOrder = new order_service.CreateOrderRequest
                {
                    CustomerName = customerName,
                    GoodsName = goodsName,
                    Amount = amount
                }
            };

            SagaContext<CreateOrderSagaData> sagaContext = null;
            try
            {
                var xid = await _sagaCollaborator.CreateGlobalTxAsync();
                _logger.LogInformation($"created xid {xid} in service {nameof(CreateOrder2)}");
                // TODO: bind xid to current request context

                // 用一个branchCaller服务去带着xid和sagaData去调用现有的SagaService的方法，
                // 从而包装好分支事务的注册
                sagaContext = new SagaContext<CreateOrderSagaData>(xid, _sagaCollaborator,
                    _sagaDataConverter, _logger);

                // 初始化saga data避免以后回滚时得到null sagaData
                
                await sagaContext.InvokeAsync(_orderService.createOrder, form);
                await sagaContext.InvokeAsync(_createOrderSaga.reserveCustomer, form);
                await sagaContext.InvokeAsync(_createOrderSaga.addLockedBalanceToMerchant, form);
                await sagaContext.InvokeAsync(_orderService.approveOrder, form);
                await sagaContext.InvokeAsync(_createOrderSaga.approveAddLockedBalanceToMerchant, form);
                await sagaContext.InvokeAsync(_createOrderSaga.addOrderHistory, form);

                await sagaContext.Commit();

                return form.OrderId;
            }
            catch (Exception e)
            {
                _logger.LogError("CreateOrder2 error", e);
                if (sagaContext != null)
                {
                    await sagaContext.Rollback();
                }
                if (form.RejectionReason != null)
                {
                    return form.RejectionReason.ToString();
                }
                return e.Message;
            }
        }

    }
}
