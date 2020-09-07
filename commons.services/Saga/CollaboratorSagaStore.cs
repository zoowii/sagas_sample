using commons.services.Sagas;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading;

namespace commons.services.Saga
{
    /**
     * saga server作为saga store
     */
    class CollaboratorSagaStore : SagaStore
    {

        private readonly SagaCollaborator _collaborator;

        protected ConcurrentDictionary<string, SagaData> _sagaDatas = new ConcurrentDictionary<string, SagaData>();

        public CollaboratorSagaStore(SagaCollaborator sagaCollaborator)
        {
            this._collaborator = sagaCollaborator;
        }

        public async Task<SagaData> GetSagaData(string sagaId)
        {
            SagaData sagaData = null;
            if (!_sagaDatas.TryGetValue(sagaId, out sagaData))
            {
                return null;
            }
            return sagaData;
        }

        public Task<SagaInfo> GetSagaInfo(string sagaId)
        {
            throw new NotImplementedException(); // TODO
        }

        public Task<IList<string>> ListSagaIdsInStates(IList<SagaState> states, int limit, string afterSagaId)
        {
            throw new NotImplementedException(); // TODO
        }

        public Task<bool> LockSagaProcess(string sagaId, string workerId, TimeSpan lockMaxTime)
        {
            // 因为通过中心化协作者，并且各分支业务和补偿都幂等，所以这里不需要加锁，提交结果时有version check
            return Task.FromResult(true);
        }

        public Task SetSagaData(string sagaId, SagaData sagaData)
        {
            _sagaDatas[sagaId] = sagaData;
            return Task.CompletedTask;
        }

        public Task<bool> SetSagaState(string sagaId, SagaState state, SagaState? oldState)
        {
            throw new NotImplementedException(); // TODO
        }

        public Task UnlockSagaProcess(string sagaId, string workerId)
        {
            return Task.CompletedTask;
        }

        Task SagaStore.CompensationDone<FormType>(SimpleSaga<FormType> saga, SagaStep step, string sagaId, SagaData form)
        {
            throw new NotImplementedException(); // TODO
        }

        Task SagaStore.CompensationException<FormType>(SimpleSaga<FormType> saga, SagaStep step, string sagaId, SagaData form, Exception e)
        {
            throw new NotImplementedException(); // TODO
        }

        Task SagaStore.CompensationStart<FormType>(SimpleSaga<FormType> saga, string sagaId, SagaData form)
        {
            throw new NotImplementedException(); // TODO
        }

        Task<string> SagaStore.CreateSagaId<FormType>(SimpleSaga<FormType> saga)
        {
            throw new NotImplementedException(); // TODO
        }
    }
}
