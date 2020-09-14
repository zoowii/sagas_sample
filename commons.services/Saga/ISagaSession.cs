using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace commons.services.Saga
{
    public interface ISagaSession
    {
        string Xid { get; }
        Task Commit();
        Task Rollback();
    }
}
