using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace commons.services.Sagas
{
    public interface SagaStore
    {
        // 开始一个新的sagaId并记录到store。如果超时没有执行完则要开启回滚流程
        Task<string> CreateSagaId<FormType>(SimpleSaga<FormType> saga) where FormType : class, SagaData;
        // 设置sagaId的状态，状态设置只有旧状态满足条件才会成功，会返回是否成功
        Task<bool> SetSagaState(string sagaId, SagaState state, SagaState? oldState);

        // 开始执行sagaId的补偿步骤（不一定是这个sagaId的第一次补偿任务执行，这个方法需要做到幂等性)
        Task CompensationStart<FormType>(SimpleSaga<FormType> saga, string sagaId, SagaData form) where FormType : class, SagaData;
        // 补偿函数调用失败记录的保存
        Task CompensationException<FormType>(SimpleSaga<FormType> saga, SagaStep step,
            string sagaId, SagaData form, Exception e) where FormType : class, SagaData;
        // 补偿函数调用成功时的保存，如果需要调用的补偿函数都调用成功了（记录step unique key or step index)，则通知saga
        Task CompensationDone<FormType>(SimpleSaga<FormType> saga, SagaStep step,
            string sagaId, SagaData form) where FormType : class, SagaData;

        // 按顺序获取满足{states}中任何一个状态的sagaId列表{limit}个，需要是在{afterSagaId}之后的，
        // afterSagaId为null表示获取这种顺序的前{limit}个
        Task<IList<string>> ListSagaIdsInStates(IList<SagaState> states, int limit, string afterSagaId);

        // 获取sagaId的当前状态等信息
        Task<SagaInfo> GetSagaInfo(string sagaId);

        Task<SagaData> GetSagaData(string sagaId);

        Task SetSagaData(string sagaId, SagaData sagaData);

        /**
         * 对某个sagaId加分布式锁，避免其他worker也在处理这个sagaId. 加锁有最大加锁时间，如果超时则自动解锁
         */
        Task<bool> LockSagaProcess(string sagaId, string workerId, TimeSpan lockMaxTime);

        Task UnlockSagaProcess(string sagaId, string workerId);
    }
}
