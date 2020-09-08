using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly ILogger _logger;
        private readonly SagaCollaborator _sagaCollaborator;
        public CollaboratorSagaWorker(ILogger logger, SagaCollaborator sagaCollaborator)
        {
            this._logger = logger;
            this._sagaCollaborator = sagaCollaborator;
        }

        private bool IsExpiredGlobalTx(QueryGlobalTransactionDetailReply globalTx)
        {
            var delta = DateTime.UtcNow - DateTimeOffset.FromUnixTimeSeconds(globalTx.CreatedAt).UtcDateTime;
            var expireTimeSpan = TimeSpan.FromSeconds(globalTx.ExpireSeconds); // saga事务超时时间
            return delta > expireTimeSpan;
        }

        public async Task DoWork()
        {
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

        private async Task ProcessUnfinishedSagaAsync(QueryGlobalTransactionDetailReply globalTx)
        {
            // 对补偿中或者补偿过程中有失败的全局事务的分支事务找到关注的分支事务（根据branchTx.serviceKey)
            // 执行各自的补偿方法，并修改分支状态
            // 如果某个分支的补偿key为空，则直接修改分支状态
            var xid = globalTx.Xid;
            var branches = globalTx.Branches;
            var watchedBranches = new List<TransactionBranchDetail>();
            foreach(var branch in branches)
            {
                var branchServiceKey = branch.BranchServiceKey;
                // TODO: 根据branchServiceResolver 找出关注的branches. 暂时关注所有branch

                watchedBranches.Add(branch);
            }
            // 如果没有关注的branches，返回
            if(watchedBranches.Count<1)
            {
                return;
            }
            // 从saga server取到saga data
            var sagaDataReply = await _sagaCollaborator.GetSagaDataAsync(xid);
            // var sagaData = sagaDataReply.Data.Span;
            // 执行回滚，以及修改分支状态
            foreach(var branch in watchedBranches)
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
                        oldBranchState, TxState.CompensationDone, oldBranchVersion, jobId, "");
                    continue;
                }
                // TODO: 调用 branchServiceResolver 去执行补偿方法
                // TODO: 调用成功后标记为已经补偿，如果调用失败或者没有找到补偿方法，上报补偿失败
            }

        }
    }
}
