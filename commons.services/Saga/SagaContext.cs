using commons.services.Sagas;
using commons.services.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace commons.services.Saga
{
    public sealed class SagaContext : IDisposable
    {
        private readonly SagaCollaborator _sagaCollaborator;
        private readonly ISagaDataConverter _sagaDataConverter;
        private readonly ISagaResolver _sagaResolver;
        private readonly ILogger _logger;

        private ISagaSession _sagaSession;

        public ISagaSession SagaSession
        {
            get
            {
                return _sagaSession;
            }
        }

        public SagaContext(SagaCollaborator sagaCollaborator,
            ISagaDataConverter sagaDataConverter,
            ISagaResolver sagaResolver,
            ILogger logger)
        {
            this._sagaCollaborator = sagaCollaborator;
            this._sagaDataConverter = sagaDataConverter;
            this._sagaResolver = sagaResolver;
            this._logger = logger;
            this._sagaSession = null;
        }

        public async Task<string> Start<T>(T form) where T : class, SagaData
        {
            var xid = await _sagaCollaborator.CreateGlobalTxAsync();
            _logger.LogInformation($"created xid {xid}");
            // bind xid to current call context
            CallContext.SetData(SagaGlobal.SAGA_XID_CONTEXT_KEY, xid);

            // 用一个branchCaller服务去带着xid和sagaData去调用现有的SagaService的方法，
            // 从而包装好分支事务的注册
            _sagaSession = new SagaSession<T>(xid, _sagaCollaborator,
                _sagaDataConverter, _sagaResolver, _logger);
            CallContext.SetData(SagaGlobal.SAGA_SESSION_CONTEXT_KEY, _sagaSession);

            // 上面这里的CallContext.SetData后的值下个线程就取不到了. 可能是因为不在同一个async CPS中?. 所以返回后要调用Bind()

            // 初始化saga data避免以后回滚时得到null sagaData
            await _sagaCollaborator.InitSagaDataAsync(xid, _sagaDataConverter.Serialize(form.GetType(), form));
            return xid;
        }

        // await Start()后需要调用Bind()在当前async CPS中绑定上下文，从而在上层的同一个async CPS中能取到sagaSession
        public void Bind()
        {
            var sagaSession = this._sagaSession;
            if (sagaSession == null)
            {
                return;
            }
            var xid = sagaSession.Xid;
            CallContext.SetData(SagaGlobal.SAGA_XID_CONTEXT_KEY, xid);
            CallContext.SetData(SagaGlobal.SAGA_SESSION_CONTEXT_KEY, _sagaSession);
        }

        public async Task Commit()
        {
            var sagaSession = this._sagaSession;
            if (sagaSession == null)
            {
                return;
            }
            await sagaSession.Commit();
        }

        public async Task Rollback()
        {
            var sagaSession = this._sagaSession;
            if (sagaSession == null)
            {
                return;
            }
            await sagaSession.Rollback();
        }
        public void Dispose()
        {
            CallContext.SetData(SagaGlobal.SAGA_XID_CONTEXT_KEY, null);
            CallContext.SetData(SagaGlobal.SAGA_SESSION_CONTEXT_KEY, null);
        }
    }
}
