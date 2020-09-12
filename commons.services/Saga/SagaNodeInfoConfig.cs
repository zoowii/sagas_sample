using System;
using System.Collections.Generic;
using System.Text;

namespace commons.services.Saga
{
    public class SagaNodeInfoConfig
    {
        public string Group { get; set; }
        public string Service { get; set; }
        public string InstanceId { get; set; }
    }
}
