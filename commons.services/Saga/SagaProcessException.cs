using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace commons.services.Sagas
{
    public class SagaProcessException : Exception
    {
        public SagaProcessException(string v) : base(v)
        { }

        public SagaProcessException(Exception e) : base(e.Message)
        { }
    }
}
