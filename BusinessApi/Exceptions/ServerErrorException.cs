using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BusinessApi.Exceptions
{
    public class ServerErrorException : Exception
    {
        public ServerErrorException(string msg) : base(msg)
        { }
    }
}
