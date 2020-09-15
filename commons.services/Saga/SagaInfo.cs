using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace commons.services.Sagas
{
    public class SagaInfo
    {
        public SagaInfo Clone()
        {
            SagaInfo c = new SagaInfo();
            c.SagaId = SagaId;
            c.State = State;
            c.FailTimes = FailTimes;
            c.SagaCreateTime = SagaCreateTime;
            c.LastProcessTime = LastProcessTime;
            c.Definition = Definition;
            return c;
        }
        public string SagaId { get; set; }
        public SagaState State { get; set; }
        public int FailTimes { get; set; } // 失败次数
        public DateTime SagaCreateTime { get; set; }

        public DateTime LastProcessTime { get; set; } // sagaId最后一次处理的时间
        public SagaDefinition Definition { get; set; }  // sagaId的定义



        public SagaInfo SetStateClone(SagaState newState)
        {
            var c = this.Clone();
            c.State = newState;
            return c;
        }

        public bool IsExpired()
        {
            var delta = DateTime.UtcNow - SagaCreateTime;
            var expireTimeSpan = TimeSpan.FromSeconds(60); // TODO: saga事务超时时间
            return delta > expireTimeSpan;
        }
    }
}
