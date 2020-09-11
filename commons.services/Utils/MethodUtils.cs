using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Linq;

namespace commons.services.Utils
{
    public class MethodUtils
    {
        public static T GetDeclaredAttribute<T>(MethodInfo method, Type attributeType) where T: Attribute
        {
            var attributes = method.GetCustomAttributes(false);
            var target =  (from attr in attributes
                                   where attr.GetType() == attributeType
                           select attr).FirstOrDefault();
            return target as T;
        }

        public static MethodInfo GetMethod(Type typeInfo, string methodName)
        {
            var methods = typeInfo.GetMethods();
            foreach(var method in methods)
            {
                if(method.Name == methodName)
                {
                    return method;
                }
            }
            return null;
        }
    }
}
