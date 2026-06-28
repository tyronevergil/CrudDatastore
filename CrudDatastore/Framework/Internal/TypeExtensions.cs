using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CrudDatastore.Framework.Internal
{
    /* http://stackoverflow.com/questions/4035719/getmethod-for-generic-method */
    internal static class TypeExtensions
    {
        public static MethodInfo GetGenericMethod(this Type type, string name, Type[] parameterTypes)
        {
            var methods = type.GetMethods();
            foreach (var method in methods.Where(m => m.Name == name && m.IsGenericMethod))
            {
                var methodParameterTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();

                if (methodParameterTypes.SequenceEqual(parameterTypes, new SimpleTypeComparer()))
                {
                    return method;
                }
            }

            return null;
        }

        public static MethodInfo GetGenericMethod(this Type type, string name, Type[] parameterTypes, Type returnType)
        {
            var methods = type.GetMethods();
            foreach (var method in methods.Where(m => m.Name == name && m.IsGenericMethod))
            {
                var methodParameterTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();

                if (methodParameterTypes.SequenceEqual(parameterTypes, new SimpleTypeComparer()))
                {
                    if (new SimpleTypeComparer().Equals(method.ReturnType, returnType))
                        return method;
                }
            }

            return null;
        }

        private class SimpleTypeComparer : IEqualityComparer<Type>
        {
            public bool Equals(Type x, Type y)
            {
                return x.Assembly == y.Assembly &&
                    x.Namespace == y.Namespace &&
                    x.Name == y.Name;
            }

            public int GetHashCode(Type obj)
            {
                throw new NotImplementedException();
            }
        }
    }
}
