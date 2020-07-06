using System;
using System.Collections.Generic;
using System.Text;

namespace apis.Exceptions
{
    public class CustomerException : Exception
    {
        public CustomerException(string message) : base(message)
        {
        }
    }
}
