using commons.services.Sagas;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace commons.services.Saga
{
    public class JsonSagaDataConverter : ISagaDataConverter
    {
        public T Deserialize<T>(Type sagaDataType, byte[] bytes) where T : class, SagaData
        {
            if(bytes == null || bytes.Length==0)
            {
                return null;
            }
            var mStr = Encoding.UTF8.GetString(bytes);
            var json = JObject.Parse(mStr);
            var dataTypeNmae = json["dataType"].ToObject<string>();
            var dataJsonStr = json["data"].ToString();
            var dataType = Type.GetType(dataTypeNmae); // TODO: 这里获取的类型不准确. 因为不是同一个程序集
            var data = JsonConvert.DeserializeObject(dataJsonStr, dataType);
            return data as T;
        }

        public byte[] Serialize(Type sagaDataType, object sagaData)
        {
            var m = new Dictionary<string, object>();
            m["dataType"] = sagaData.GetType().FullName;
            m["data"] = sagaData;
            var mStr = JsonConvert.SerializeObject(m);
            return Encoding.UTF8.GetBytes(mStr);
        }
    }
}
