using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace commons.services.Saga
{
    public sealed class SagaGlobal
    {
        public const string SAGA_XID_CONTEXT_KEY = "saga_xid"; // saga xid在CallContext中的上下文变量
        public const string SAGA_CONTEXT_CONTEXT_KEY = "saga_context"; // sagaContext在CallContext中的上下文变量


        private static ConcurrentDictionary<string, Type> _bindedSagaDataTypes = new ConcurrentDictionary<string, Type>();

        public static void BindSagaDataType(Type sagaDataType)
        {
            var fullTypeName = sagaDataType.FullName;
            _bindedSagaDataTypes[fullTypeName] = sagaDataType;
        }

        public static Type ResolveSagaDataType(string fullTypeName)
        {
            return _bindedSagaDataTypes.GetValueOrDefault(fullTypeName, null);
        }
    }
}
