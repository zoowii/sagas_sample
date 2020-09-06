using System;
using System.Collections.Generic;
using System.Text;

namespace commons.services.Saga
{
    public class SagaServerException : Exception
    {
        public SagaServerException(string v) : base(v)
        { }

        public SagaServerException(Exception e) : base(e.Message)
        { }
    }
}
