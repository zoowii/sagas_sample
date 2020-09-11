using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace commons.services.Saga
{
    class SagaGlobal
    {

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
