using commons.services.Sagas;
using System;
using System.Collections.Generic;
using System.Text;

namespace commons.services.Saga
{
    /**
     * saga data的序列化和反序列化的接口
     */
    public interface ISagaDataConverter
    {
        byte[] Serialize(Type sagaDataType, object sagaData);

        T Deserialize<T>(Func<string, Type> typeResolver, byte[] bytes) where T : class, SagaData;
    }
}
