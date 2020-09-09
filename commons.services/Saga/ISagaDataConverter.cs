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

        // TODO: 反序列化时改成传入一个空的非null对象
        T Deserialize<T>(Type sagaDataType, byte[] bytes) where T : class, SagaData;
    }
}
