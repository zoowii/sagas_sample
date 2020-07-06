using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace commons.services.Sagas
{
    public interface SagaWorker
    {
        SagaStore GetStore();


        // 处理某个未完成的sagaId（执行补偿）
        Task ProcessUnfinishedSagaAsync(string sagaId, SagaData sagaData);


        /**
         * 定时找出超时或者失败的sagaId，或者执行失败的补偿任务，开始执行补偿任务或者重试补偿（不超过限定次数的话）
         * @param limit 每次处理的sagaIds数量
         * @param lastProcessSagaId 上次处理的sagaId，本次从这个sagaId之后的sagaIds序列开始处理
         */
        Task<string> ProcessSomeUnfinishedSagasAsync(int limit, string lastProcessSagaId);
    }
}
