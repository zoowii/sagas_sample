using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using saga_server;

namespace commons.services.Saga
{
    public class SagaCollaborator
    {
        private readonly SagaServer.SagaServerClient _client;
        private readonly NodeInfo _nodeInfo;
        public SagaCollaborator(SagaServer.SagaServerClient client, NodeInfo nodeInfo)
        {
            this._client = client;
            this._nodeInfo = nodeInfo;
        }

        private const int OkCode = 0;
        private const int ResourceChangedErrorCode = 3;

        public async Task<string> CreateGlobalTxAsync()
        {
            var expireSeconds = 60;
            var reply = await _client.CreateGlobalTransactionAsync(
                new CreateGlobalTransactionRequest()
                {
                Node= _nodeInfo,
                ExpireSeconds = expireSeconds,
                Extra = ""
                });
            if(reply.Code!= OkCode)
            {
                throw new SagaServerException(reply.Error);
            }
            return reply.Xid;
        }

        public async Task<string> CreateBranchTxAsync(string xid, string branchServiceKey, string branchCompensationServiceKey)
        {
            var reply = await _client.CreateBranchTransactionAsync(
                new CreateBranchTransactionRequest()
                {
                    Node = _nodeInfo,
                    BranchServiceKey = branchServiceKey,
                    BranchCompensationServiceKey = branchCompensationServiceKey,
                    Xid = xid
                });
            if (reply.Code != OkCode)
            {
                throw new SagaServerException(reply.Error);
            }
            return reply.BranchId;
        }

        public async Task<QueryGlobalTransactionDetailReply> QueryGlobalTxAsync(string xid)
        {
            var reply = await _client.QueryGlobalTransactionDetailAsync(
                new QueryGlobalTransactionDetailRequest()
                {
                    Xid = xid
                });
            if (reply.Code != OkCode)
            {
                throw new SagaServerException(reply.Error);
            }
            return reply;
        }

        public async Task<QueryBranchTransactionDetailReply> QueryBranchTxAsync(string branchTxId)
        {
            var reply = await _client.QueryBranchTransactionDetailAsync(
                new QueryBranchTransactionDetailRequest()
                {
                    BranchId = branchTxId
                });
            if (reply.Code != OkCode)
            {
                throw new SagaServerException(reply.Error);
            }
            return reply;
        }

        // 乐观的修改global tx state，如果version老了就重试
        public async Task<TxState> SubmitGlobalTxStateOptimismAsync(
            string xid, TxState state)
        {
            var tryCount = 0;
            var maxTryTimes = 10;
            do
            {
                tryCount++;
                if(tryCount> maxTryTimes)
                {
                    throw new SagaServerException("retry too many times but version expired");
                }
                var detail = await QueryGlobalTxAsync(xid);
                var reply = await _client.SubmitGlobalTransactionStateAsync(
                    new SubmitGlobalTransactionStateRequest()
                    {
                        Xid = xid,
                        OldState = detail.State,
                        State = state,
                        OldVersion = detail.Version
                    });
                if(reply.Code == ResourceChangedErrorCode)
                {
                    continue;
                }
                if (reply.Code != OkCode)
                {
                    throw new SagaServerException(reply.Error);
                }
                return reply.State;
            } while (true);
        }

        public async Task<TxState> SubmitGlobalTxStateAsync(
            string xid, TxState oldState, TxState state, int oldVersion)
        {
            var reply = await _client.SubmitGlobalTransactionStateAsync(
                new SubmitGlobalTransactionStateRequest()
                {
                    Xid = xid,
                    OldState = oldState,
                    State = state,
                    OldVersion = oldVersion
                });
            if (reply.Code != OkCode)
            {
                throw new SagaServerException(reply.Error);
            }
            return reply.State;
        }

        public async Task<TxState> SubmitBranchTxStateAsync(
            string xid, string branchTxId, TxState oldState, TxState state, int oldVersion, string jobId, string errorReason)
        {
            var reply = await _client.SubmitBranchTransactionStateAsync(
                new SubmitBranchTransactionStateRequest()
                {
                    Xid = xid,
                    BranchId = branchTxId,
                    OldState = oldState,
                    State = state,
                    OldVersion = oldVersion,
                    JobId = jobId,
                    ErrorReason = errorReason
                });
            if (reply.Code != OkCode)
            {
                throw new SagaServerException(reply.Error);
            }
            return reply.State;
        }

    }
}
