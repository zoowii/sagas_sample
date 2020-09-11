using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Linq;
using System.Linq.Expressions;

namespace commons.services.Utils
{
    public class DelegateBuilder
    {
        public static T BuildDelegate<T>(MethodInfo method, Type thisType, params object[] missingParamValues)
        {
            var queueMissingParams = new Queue<object>(missingParamValues);

            var dgtMi = typeof(T).GetMethod("Invoke");
            var dgtRet = dgtMi.ReturnType;
            var dgtParams = dgtMi.GetParameters();

            var paramsOfDelegate = dgtParams
                .Select(tp => Expression.Parameter(tp.ParameterType, tp.Name))
                .ToArray();
            var thisTypeDelegate = Expression.Parameter(thisType, "this");

            var methodParams = method.GetParameters();

            if (method.IsStatic)
            {
                var paramsToPass = methodParams
                    .Select((p, i) => CreateParam(paramsOfDelegate, i, p, queueMissingParams))
                    .ToArray();

                var expr = Expression.Lambda<T>(
                    Expression.Call(method, paramsToPass),
                    paramsOfDelegate);

                return expr.Compile();
            }
            else
            {
                var paramThis = Expression.Convert(thisTypeDelegate, method.DeclaringType);


                var allArgsDelegate = new ParameterExpression[1 + paramsOfDelegate.Length];
                allArgsDelegate[0] = thisTypeDelegate;
                for (int i = 0; i < paramsOfDelegate.Length; i++)
                {
                    allArgsDelegate[i + 1] = paramsOfDelegate[i];
                }

                var paramsToPass = methodParams
                    .Select((p, i) => CreateParam(allArgsDelegate, i+1, p, queueMissingParams))
                    .ToArray();


                var expr = Expression.Lambda<T>(
                    Expression.Call(paramThis, method, paramsToPass),
                    paramsOfDelegate);

                return expr.Compile();
            }
        }

        private static Expression CreateParam(ParameterExpression[] paramsOfDelegate, int i, ParameterInfo callParamType, Queue<object> queueMissingParams)
        {
            if (i < paramsOfDelegate.Length)
                return Expression.Convert(paramsOfDelegate[i], callParamType.ParameterType);

            if (queueMissingParams.Count > 0)
                return Expression.Constant(queueMissingParams.Dequeue());

            if (callParamType.ParameterType.IsValueType)
                return Expression.Constant(Activator.CreateInstance(callParamType.ParameterType));

            return Expression.Constant(null);
        }
    }
}
