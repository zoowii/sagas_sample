using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using saga_server;

namespace commons.services.Saga
{
    public class SagaCollaborator
    {
        public SagaServer.SagaServerClient Client { get; set; }
        public NodeInfo NodeInfo { get; set; }

        private const int OkCode = 0;
        private const int ResourceChangedErrorCode = 3;

        public async Task<string> CreateGlobalTxAsync()
        {
            var expireSeconds = 60;
            var reply = await Client.CreateGlobalTransactionAsync(
                new CreateGlobalTransactionRequest()
                {
                    Node = NodeInfo,
                    ExpireSeconds = expireSeconds,
                    Extra = ""
                });
            if (reply.Code != OkCode)
            {
                throw new SagaServerException(reply.Error);
            }
            return reply.Xid;
        }

        public async Task<string> CreateBranchTxAsync(string xid, string branchServiceKey, string branchCompensationServiceKey)
        {
            var reply = await Client.CreateBranchTransactionAsync(
                new CreateBranchTransactionRequest()
                {
                    Node = NodeInfo,
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
            var reply = await Client.QueryGlobalTransactionDetailAsync(
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
            var reply = await Client.QueryBranchTransactionDetailAsync(
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
                if (tryCount > maxTryTimes)
                {
                    throw new SagaServerException("retry too many times but version expired");
                }
                var detail = await QueryGlobalTxAsync(xid);
                var reply = await Client.SubmitGlobalTransactionStateAsync(
                    new SubmitGlobalTransactionStateRequest()
                    {
                        Xid = xid,
                        OldState = detail.State,
                        State = state,
                        OldVersion = detail.Version
                    });
                if (reply.Code == ResourceChangedErrorCode)
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
            var reply = await Client.SubmitGlobalTransactionStateAsync(
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
            string xid, string branchTxId, TxState oldState, TxState state, int oldVersion,
            string jobId, string errorReason, byte[] sagaData)
        {
            ByteString sagaDataByteString = null;
            if(sagaData != null)
            {
                using(var ms = new MemoryStream(sagaData))
                {
                    sagaDataByteString = ByteString.FromStream(ms);
                }
            }
            var reply = await Client.SubmitBranchTransactionStateAsync(
                new SubmitBranchTransactionStateRequest()
                {
                    Xid = xid,
                    BranchId = branchTxId,
                    OldState = oldState,
                    State = state,
                    OldVersion = oldVersion,
                    JobId = jobId,
                    ErrorReason = errorReason,
                    SagaData = sagaDataByteString
                });
            if (reply.Code != OkCode)
            {
                throw new SagaServerException(reply.Error);
            }
            return reply.State;
        }

        public async Task<IList<string>> ListGlobalTransactionsOfStatesAsync(
            IEnumerable<TxState> states, int limit)
        {
            var req = new ListGlobalTransactionsOfStatesRequest
            {
                Limit = limit
            };
            req.States.AddRange(states);
            var reply = await Client.ListGlobalTransactionsOfStatesAsync(req);
            if (reply.Code != OkCode)
            {
                throw new SagaServerException(reply.Error);
            }
            return reply.Xids;
        }

        public async Task InitSagaDataAsync(
            string xid, byte[] data)
        {
            using (var dataStream = new MemoryStream(data))
            {
                var reply = await Client.InitSagaDataAsync(new InitSagaDataRequest()
                {
                    Xid = xid,
                    Data = ByteString.FromStream(dataStream)
                });
                if (reply.Code != OkCode)
                {
                    throw new SagaServerException(reply.Error);
                }
            }
        }

        public async Task<GetSagaDataReply> GetSagaDataAsync(
            string xid)
        {
            var reply = await Client.GetSagaDataAsync(new GetSagaDataRequest()
            {
                Xid = xid
            });
            if (reply.Code != OkCode)
            {
                throw new SagaServerException(reply.Error);
            }
            return reply;
        }

    }
}
