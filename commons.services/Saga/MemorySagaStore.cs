using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace commons.services.Sagas
{
    public class MemorySagaStore : SagaStore
    {
        // saga中已经成功的补偿steps的keys. sagaId => done stepKeys
        private ConcurrentDictionary<string, ISet<string>> sagaDoneCompensationSteps = new ConcurrentDictionary<string, ISet<string>>();
        // saga中失败的补偿step的失败次数. sagaId+step key => fail count
        private ConcurrentDictionary<string, int> sagaStepsCompensationFailCount = new ConcurrentDictionary<string, int>();

        public async Task CompensationDone<FormType>(SimpleSaga<FormType> saga,
            SagaStep step, string sagaId, SagaData form) where FormType : class, SagaData
        {
            var stepKey = saga.GetSagaDefinition().KeyOfStep(step);
            sagaDoneCompensationSteps.AddOrUpdate(sagaId, new HashSet<string>() { stepKey }, (_, oldSet) => {
                oldSet.Add(stepKey);
                return oldSet;
            });
            var doneCompensationSteps = sagaDoneCompensationSteps[sagaId];
            var needDoCompensationStepsCount = saga.GetSagaDefinition().Steps.Count(); // 此sagaId需要执行补偿的steps count
            if(doneCompensationSteps.Count()>= needDoCompensationStepsCount)
            {
                // sagaStates[sagaId] = SagaState.COMPENSATION_DONE;
                var oldInfo = _sagaInfos[sagaId];
                if (oldInfo.State != SagaState.COMPENSATION_DONE)
                {
                    if (!_sagaInfos.TryUpdate(sagaId, oldInfo.SetStateClone(SagaState.COMPENSATION_DONE), oldInfo))
                    {
                        throw new SagaAbortException($"saga {sagaId} CompensationDone error because of state update conflict");
                    }
                }
                saga.OnSagaRolledBack(sagaId, form);
            }
            return;
        }

        protected string makeSagaIdAndStepKey<FormType>(string sagaId,
            SimpleSaga<FormType> saga, SagaStep step) where FormType: class, SagaData
        {
            var stepKey = saga.GetSagaDefinition().KeyOfStep(step);
            return $"{sagaId}-{stepKey}";
        }

        protected int SINGLE_STEP_COMPENSATION_TRY_COUNT = 3; // 一个步骤的补偿任务最多重试的次数

        public Task CompensationException<FormType>(SimpleSaga<FormType> saga,
            SagaStep step, string sagaId, SagaData form, Exception e) where FormType : class, SagaData
        {
            // 记录失败信息和失败次数
            var key = makeSagaIdAndStepKey(sagaId, saga, step);
            sagaStepsCompensationFailCount.AddOrUpdate(key, 1, (_, oldCount) => oldCount + 1);
            var failCount = sagaStepsCompensationFailCount[key];
            if(failCount>= SINGLE_STEP_COMPENSATION_TRY_COUNT)
            {
                // 这个step的补偿任务执行失败次数太多了
                Console.WriteLine($"saga {sagaId} step {saga.GetSagaDefinition().KeyOfStep(step)} compensation fail too many times error {e.Message}");
                // sagaStates.TryUpdate(sagaId, SagaState.COMPENSATION_FAIL, SagaState.COMPENSATION_DOING);
                while (true)
                {
                    if(!_sagaInfos.ContainsKey(sagaId))
                    {
                        break;
                    }
                    var oldInfo = _sagaInfos[sagaId];
                    if(oldInfo.State.IsEndState())
                    {
                        break;
                    }
                    if(_sagaInfos.TryUpdate(sagaId, oldInfo.SetStateClone(SagaState.COMPENSATION_FAIL), oldInfo))
                    {
                        break;
                    }
                }
            }
            else
            {
                while(true)
                {
                    if (!_sagaInfos.ContainsKey(sagaId))
                    {
                        break;
                    }
                    var oldInfo = _sagaInfos[sagaId];
                    if (oldInfo.State.IsEndState() || oldInfo.State == SagaState.COMPENSATION_ERROR)
                    {
                        break;
                    }
                    if (_sagaInfos.TryUpdate(sagaId, oldInfo.SetStateClone(SagaState.COMPENSATION_ERROR), oldInfo))
                    {
                        break;
                    }
                }
            }
            return Task.CompletedTask;
        }

        public async Task<string> CreateSagaId<FormType>(SimpleSaga<FormType> saga) where FormType : class, SagaData
        {
            var sagaId = Guid.NewGuid().ToString();
            //sagaIdsStartTimes[sagaId] = DateTime.UtcNow;
            //sagaStates[sagaId] = SagaState.PROCESSING;
            var now = DateTime.UtcNow;
            var definition = saga.GetSagaDefinition();
            try
            {
                var sagaInfo = new SagaInfo
                {
                    SagaId = sagaId,
                    State = SagaState.PROCESSING,
                    FailTimes = 0,
                    SagaCreateTime = now,
                    LastProcessTime = now,
                    Definition = definition
                };
                _sagaInfos[sagaId] = sagaInfo;
            } catch(Exception e)
            {
                Console.WriteLine($"erro {e.Message}");
            }
            return sagaId;
        }

        // 各sagaId的状态等信息, sagaId => SagaInfo
        protected ConcurrentDictionary<string, SagaInfo> _sagaInfos = new ConcurrentDictionary<string, SagaInfo>();


        public async Task<bool> SetSagaState(string sagaId, SagaState state, SagaState? oldState)
        {
            if(oldState == null)
            {
                _sagaInfos[sagaId] = _sagaInfos[sagaId].SetStateClone(state);
                return true;
            }
            if(!_sagaInfos.ContainsKey(sagaId))
            {
                return true;
            }
            var oldInfo = _sagaInfos[sagaId];
            return _sagaInfos.TryUpdate(sagaId, oldInfo.SetStateClone(state), oldInfo);
        }

        public async Task CompensationStart<FormType>(SimpleSaga<FormType> saga,
            string sagaId, SagaData form) where FormType : class, SagaData
        {
            if(!await SetSagaState(sagaId, SagaState.COMPENSATION_DOING, null))
            {
                throw new SagaAbortException($"sagaId {sagaId} CompensationStart error because of set state conflict");
            }
        }
        public async Task<IList<string>> ListSagaIdsInStates(IList<SagaState> states, int limit, string afterSagaId)
        {
            var result = new List<string>();
            if (afterSagaId != null)
            {
                return result;
            }
            foreach(var p in _sagaInfos)
            {
                if(states.Contains(p.Value.State))
                {
                    result.Add(p.Key);
                }
            }
            return result;
        }

        public async Task<SagaInfo> GetSagaInfo(string sagaId)
        {
            if(!_sagaInfos.ContainsKey(sagaId))
            {
                return null;
            }
            var info = _sagaInfos[sagaId];
            return info;
        }

        private ConcurrentDictionary<string, Mutex> sagaProcessLocks = new ConcurrentDictionary<string, Mutex>();

        public async Task<bool> LockSagaProcess(string sagaId, string workerId, TimeSpan lockMaxTime)
        {
            // 因为目前memory store模式下worker是单进程单线程，所以直接使用互斥锁即可，分布式场景可以用redis/zk等其他分布式锁方案
            var sagaLock = sagaProcessLocks.GetOrAdd(sagaId, (sid) => new Mutex(true));
            if(!sagaLock.WaitOne(100))
            {
                return false;
            }
            return true;
        }

        public Task UnlockSagaProcess(string sagaId, string workerId)
        {
            Mutex sagaLock = null;
            if(!sagaProcessLocks.TryGetValue(sagaId, out sagaLock))
            {
                return Task.CompletedTask;
            }
            sagaLock.ReleaseMutex();
            return Task.CompletedTask;
        }

        protected ConcurrentDictionary<string, SagaData> _sagaDatas = new ConcurrentDictionary<string, SagaData>();

        public async Task<SagaData> GetSagaData(string sagaId)
        {
            SagaData sagaData = null;
            if(!_sagaDatas.TryGetValue(sagaId, out sagaData))
            {
                return null;
            }
            return sagaData;
        }

        public Task SetSagaData(string sagaId, SagaData sagaData)
        {
            _sagaDatas[sagaId] = sagaData;
            return Task.CompletedTask;
        }
    }
}
