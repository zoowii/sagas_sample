using System;
using System.Collections.Generic;
using System.Text;

namespace commons.services.Saga
{
    /**
     * 适配协作者saga server的saga worker，从saga server获取未完成的xids，
     * 找到需要自己处理的branches的补偿方法，以及超时事务，做相应处理
     */
    public class CollaboratorSagaWorker
    {
        // TODO
    }
}
