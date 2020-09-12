using commons.services.Sagas;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Logging;
using commons.services.Utils;

namespace commons.services.Saga
{
    public class SagaContext<FormType> where FormType : class, SagaData
    {
        public delegate Task StepCallback(FormType form);

        private readonly string _xid;
        private readonly SagaCollaborator _sagaCollaborator;
        private readonly ISagaDataConverter _sagaDataConverter;
        private readonly ISagaResolver _sagaResolver;
        private readonly ILogger _logger;

        public SagaContext(string xid,
            SagaCollaborator sagaCollaborator,
            ISagaDataConverter sagaDataConverter,
            ISagaResolver sagaResolver,
            ILogger logger)
        {
            this._xid = xid;
            this._sagaCollaborator = sagaCollaborator;
            this._sagaDataConverter = sagaDataConverter;
            this._sagaResolver = sagaResolver;
            this._logger = logger;
        }

        public string Xid { get { return _xid; } }

        private class BranchStepInfo
        {
            public string BranchServiceKey { get; set; }
            public string BranchCompensationServiceKey { get; set; }
        }

        // 缓存的branchStepCallbackMethod => BranchStepInfo
        private ConcurrentDictionary<MethodInfo, BranchStepInfo> _cachedBranchStepInfos = new ConcurrentDictionary<MethodInfo, BranchStepInfo>();
        
        private BranchStepInfo getBranchStepInfo(StepCallback branchStep)
        {
            return _cachedBranchStepInfos.GetOrAdd(branchStep.Method, (method) =>
            {
                var methodName = method.Name;
                var compensableAttr = MethodUtils.GetDeclaredAttribute<Compensable>(method, typeof(Compensable));
                var branchServiceKey = _sagaResolver.GetServiceKey(method.DeclaringType, methodName);
                var branchCompensationServiceKey = "";
                if (compensableAttr != null)
                {
                    var compensationMethodName = (compensableAttr as Compensable).ActionName;
                    branchCompensationServiceKey = _sagaResolver.GetServiceKey(method.DeclaringType, compensationMethodName);
                }
                return new BranchStepInfo()
                {
                    BranchServiceKey = branchServiceKey,
                    BranchCompensationServiceKey = branchCompensationServiceKey
                };
            });
        }

        public async Task InvokeAsync(StepCallback branchStep, FormType sagaData)
        {
            // 注册branch tx id
            var branchStepInfo = getBranchStepInfo(branchStep);

            var branchTxId = await _sagaCollaborator.CreateBranchTxAsync(_xid,
                branchStepInfo.BranchServiceKey,
                branchStepInfo.BranchCompensationServiceKey);

            // 本次执行的jobId
            var jobId = Guid.NewGuid().ToString();

            _logger.LogInformation($"created branch txid {branchTxId} jobId {jobId} in xid {_xid}");

            
            try
            {   
                // invoke branchStep
                await branchStep(sagaData);

                // 调用成功，通知saga server状态变化
                var oldBranchTxDetail = await _sagaCollaborator.QueryBranchTxAsync(branchTxId);
                var sagaDataBytes = _sagaDataConverter.Serialize(sagaData.GetType(), sagaData);

                var newState = await _sagaCollaborator.SubmitBranchTxStateAsync(_xid, branchTxId,
                    oldBranchTxDetail.Detail.State, saga_server.TxState.Committed,
                    oldBranchTxDetail.Detail.Version, jobId, "",
                    sagaDataBytes);

                _logger.LogInformation($"branch txid {branchTxId} state changed to {newState}");
            }
            catch (Exception e)
            {
                _logger.LogError($"invoke branch service {branchStepInfo.BranchServiceKey} error", e);
                // 如果有异常，通知saga server回滚后台执行回滚,然后再抛出异常
                var oldBranchTxDetail = await _sagaCollaborator.QueryBranchTxAsync(branchTxId);
                await _sagaCollaborator.SubmitBranchTxStateAsync(_xid, branchTxId,
                    oldBranchTxDetail.Detail.State, saga_server.TxState.CompensationDoing,
                    oldBranchTxDetail.Detail.Version, jobId, e.Message, null);

                throw e;
            }
            
            return;
        }

        // 提交全局事务
        public async Task Commit()
        {
            await _sagaCollaborator.SubmitGlobalTxStateOptimismAsync(_xid, saga_server.TxState.Committed);
            return;
        }

        // 回滚本全局事务
        public async Task Rollback()
        {
            await _sagaCollaborator.SubmitGlobalTxStateOptimismAsync(_xid, saga_server.TxState.CompensationDoing);
            return;
        }

    }
}
