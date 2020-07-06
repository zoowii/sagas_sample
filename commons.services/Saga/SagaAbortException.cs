using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace commons.services.Sagas
{
    public class SagaAbortException : Exception
    {

        public SagaAbortException(string v) : base(v)
        { }

        public SagaAbortException(Exception e) : base(e.Message)
        { }
    }
}
