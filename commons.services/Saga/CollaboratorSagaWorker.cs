using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using commons.services.Sagas;
using Microsoft.Extensions.Logging;
using saga_server;

namespace commons.services.Saga
{
    /**
     * 适配协作者saga server的saga worker，从saga server获取未完成的xids，
     * 找到需要自己处理的branches的补偿方法，以及超时事务，做相应处理
     */
    public class CollaboratorSagaWorker
    {
        private readonly ILogger<CollaboratorSagaWorker> _logger;
        private readonly SagaCollaborator _sagaCollaborator;
        private readonly ISagaResolver _branchServiceResolver;
        private readonly ISagaDataConverter _sagaDataConverter;
        public CollaboratorSagaWorker(ILogger<CollaboratorSagaWorker> logger,
            SagaCollaborator sagaCollaborator,
            ISagaResolver branchServiceResolver,
            ISagaDataConverter sagaDataConverter)
        {
            this._logger = logger;
            this._sagaCollaborator = sagaCollaborator;
            this._branchServiceResolver = branchServiceResolver;
            this._sagaDataConverter = sagaDataConverter;
        }

        private bool IsExpiredGlobalTx(QueryGlobalTransactionDetailReply globalTx)
        {
            var delta = DateTime.UtcNow - DateTimeOffset.FromUnixTimeSeconds(globalTx.CreatedAt).UtcDateTime;
            var expireTimeSpan = TimeSpan.FromSeconds(globalTx.ExpireSeconds); // saga事务超时时间
            return delta > expireTimeSpan;
        }

        public async Task DoWork()
        {
            if(_sagaCollaborator.Client == null)
            {
                return;
            }
            var limit = 1000;
            var xids = await _sagaCollaborator.ListGlobalTransactionsOfStatesAsync(new TxState[] {
                TxState.Processing, TxState.CompensationDoing, TxState.CompensationError
            }, limit);
            _logger.LogInformation($"there are {xids.Count} unfinished xids to process");
            if (xids.Count < 1)
            {
                return;
            }
            foreach (var xid in xids)
            {
                try
                {
                    var globalTx = await _sagaCollaborator.QueryGlobalTxAsync(xid);
                    if (globalTx.State == TxState.Processing)
                    {
                        // 如果是正常处理中的sagaId，如果没有超时，则不做补偿等后续操作
                        if (!IsExpiredGlobalTx(globalTx))
                        {
                            continue;
                        }

                        // 超时的全局交易要进入回滚状态
                        await _sagaCollaborator.SubmitGlobalTxStateAsync(xid, globalTx.State, TxState.CompensationDoing, globalTx.Version);
                        continue;
                    }
                    _logger.LogInformation($"workerstart process saga {xid}");
                    await ProcessUnfinishedSagaAsync(globalTx);
                    _logger.LogInformation($"worker process saga {xid} done");
                }
                catch (Exception e)
                {
                    _logger.LogError($"process unifinished saga {xid} error {e.Message}");
                }
            }
        }

        private Type sagaDataTypeResolver(string typeFullName)
        {
            return SagaGlobal.ResolveSagaDataType(typeFullName);
        }

        private async Task ProcessUnfinishedSagaAsync(QueryGlobalTransactionDetailReply globalTx)
        {
            // 对补偿中或者补偿过程中有失败的全局事务的分支事务找到关注的分支事务（根据branchTx.serviceKey)
            // 执行各自的补偿方法，并修改分支状态
            // 如果某个分支的补偿key为空，则直接修改分支状态
            var xid = globalTx.Xid;
            var branches = globalTx.Branches;
            var watchedBranches = new List<TransactionBranchDetail>();
            Dictionary<string, Func<object, Task>> serviceMethods = new Dictionary<string, Func<object, Task>>();
            foreach(var branch in branches)
            {
                var branchServiceKey = branch.BranchServiceKey;
                // 根据branchServiceResolver 找出关注的branches. 暂时关注所有branch
                var branchService = _branchServiceResolver.ResolveBranch(branchServiceKey);
                if(branchService == null)
                {
                    continue;
                }
                watchedBranches.Add(branch);
                serviceMethods[branchServiceKey] = branchService;
            }
            // 如果没有关注的branches，返回
            if(watchedBranches.Count<1)
            {
                return;
            }
            watchedBranches.Reverse(); // watchedBranches改成倒叙，这是为了优先执行补偿后到的分支
            // 执行回滚，以及修改分支状态
            foreach (var branch in watchedBranches)
            {
                var branchId = branch.BranchId;
                var branchCompensationServiceKey = branch.BranchCompensationServiceKey;
                var oldBranchState = branch.State;
                var oldBranchVersion = branch.Version;
                var jobId = Guid.NewGuid().ToString();
                if(branchCompensationServiceKey==null || branchCompensationServiceKey.Length<1)
                {
                    // 补偿方法为空，直接标记为已经补偿
                    await _sagaCollaborator.SubmitBranchTxStateAsync(xid, branchId,
                        oldBranchState, TxState.CompensationDone, oldBranchVersion, jobId, "", null);
                    continue;
                }
                // 调用 branchServiceResolver 去执行补偿方法
                var branchCompensationService = _branchServiceResolver.ResolveBranch(branchCompensationServiceKey);

                // 调用成功后标记为已经补偿，如果调用失败或者没有找到补偿方法，上报补偿失败
                var compensationSuccess = false;
                var errorReason = "";
                if(branchCompensationService != null)
                {
                    try
                    {

                        // 从saga server取到saga data
                        var sagaDataReply = await _sagaCollaborator.GetSagaDataAsync(xid);
                        byte[] sagaDataBytes = sagaDataReply.Data.ToByteArray();

                        var sagaData = _sagaDataConverter.Deserialize<SagaData>(sagaDataTypeResolver, sagaDataBytes);
                        await branchCompensationService(sagaData);
                        var changedSagaDataBytes = _sagaDataConverter.Serialize(typeof(SagaData), sagaData);


                        await _sagaCollaborator.SubmitBranchTxStateAsync(xid, branchId, branch.State,
                            TxState.CompensationDone, branch.Version, jobId, "", changedSagaDataBytes);

                        compensationSuccess = true;
                    } catch(Exception e)
                    {
                        compensationSuccess = false;
                        errorReason = e.Message;
                        _logger.LogError($"branch {branchId} compensation error", e);
                    }
                }
                if(!compensationSuccess)
                {

                    _logger.LogError($"branch compensation {branchCompensationServiceKey} fail");
                    try
                    {
                        var latestBranch = await _sagaCollaborator.QueryBranchTxAsync(branchId);
                        await _sagaCollaborator.SubmitBranchTxStateAsync(xid,
                            branchId, latestBranch.Detail.State, TxState.CompensationError,
                            latestBranch.Detail.Version, jobId, errorReason, null);
                    }
                    catch (Exception e2)
                    {
                        _logger.LogError($"SubmitBranchTxStateAsync o branch {branchId} error", e2);
                    }
                }
            }

        }
    }
}
