using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace commons.services.Sagas
{
    public enum SagaState
    {
        PROCESSING, // 正常处理事务中
        SUCCESS, // 整个sagaId处理成功
        COMPENSATION_DOING, // 补偿任务执行中
        COMPENSATION_ERROR, // 补偿任务某次执行失败
        COMPENSATION_DONE, // 补偿任务执行完成
        COMPENSATION_FAIL // 补偿任务多次执行过程整体失败
    }
    public static class SagaStateExtension
    {
        public static bool IsEndState(this SagaState state)
        {
            return state == SagaState.SUCCESS || state == SagaState.COMPENSATION_DONE || state == SagaState.COMPENSATION_FAIL;
        }
    }
}
