using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace commons.services.Sagas
{
    public class SimpleSagaWorker : SagaWorker
    {
        protected readonly SagaStore sagaStore;
        protected readonly ILogger<SimpleSagaWorker> _logger;
        protected readonly string _workerId;

        public SimpleSagaWorker(SagaStore sagaStore, ILogger<SimpleSagaWorker> logger)
        {
            this.sagaStore = sagaStore;
            this._logger = logger;
            this._workerId = Guid.NewGuid().ToString();
        }

        public SagaStore GetStore()
        {
            return sagaStore;
        }

        public async Task<string> ProcessSomeUnfinishedSagasAsync(int limit, string lastProcessSagaId)
        {
            var sagaIds = await sagaStore.ListSagaIdsInStates(new List<SagaState>(){
                SagaState.PROCESSING, SagaState.COMPENSATION_DOING, SagaState.COMPENSATION_ERROR
            }, limit, lastProcessSagaId);
            _logger.LogInformation($"there are {sagaIds.Count} unfinished sagaIds to process");
            if (sagaIds.Count < 1)
            {
                return null;
            }
            foreach(var sagaId in sagaIds)
            {
                try
                {
                    var sagaInfo = await sagaStore.GetSagaInfo(sagaId);
                    if(sagaInfo.State == SagaState.PROCESSING)
                    {
                        // 如果是正常处理中的sagaId，如果没有超时，则不做补偿等后续操作
                        if(!sagaInfo.IsExpired())
                        {
                            continue;
                        }
                    }
                    try
                    {
                        if (!await sagaStore.LockSagaProcess(sagaId, _workerId, TimeSpan.FromSeconds(60))) {
                            throw new SagaProcessException($"worker {_workerId} lock saga {sagaId} to process error");
                        }
                        _logger.LogInformation($"worker {_workerId} start process saga {sagaId}");
                        SagaData sagaData = await sagaStore.GetSagaData(sagaId);
                        await ProcessUnfinishedSagaAsync(sagaId, sagaData);
                        _logger.LogInformation($"worker {_workerId} process saga {sagaId} done");
                    }
                    finally
                    {
                        await sagaStore.UnlockSagaProcess(sagaId, _workerId);
                    }
                }
                catch(Exception e)
                {
                    _logger.LogError($"process unifinished saga {sagaId} error {e.Message}");
                }
            }
            return sagaIds[sagaIds.Count - 1];
        }

        public async Task ProcessUnfinishedSagaAsync(string sagaId, SagaData sagaData)
        {
            var sagaInfo = await sagaStore.GetSagaInfo(sagaId);
            if (sagaInfo == null)
            {
                return;
            }
            if (sagaInfo.State.IsEndState())
            {
                return;
            }
            var sagaDefinition = sagaInfo.Definition;
            var saga = sagaDefinition.Saga;
            await saga.DoCompensationOfSaga(sagaId, sagaData);
        }
    }
}
