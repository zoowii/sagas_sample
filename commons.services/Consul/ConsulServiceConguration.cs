using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace commons.services.Consul
{
    public class ConsulServiceConguration
    {
        public const string Section = "Consul";

        public string ConsulUrl { get; set; }
        public string ServiceName { get; set; }
        public string ServiceUrl { get; set; }

        public string[] Tags { get; set; }

        public int TTLSeconds { get; set; }
        public int TimeoutSeconds { get; set; }

    }
}
