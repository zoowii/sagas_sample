using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BusinessApi.Sagas
{
    public enum CreateOrderRejectionReason
    {
        INSUFFICIENT_CREDIT,
        UNKNOWN_CUSTOMER,
        UNKNOWN_ERROR
    }
}
