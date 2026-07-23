using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace CrudDatastore.Framework.Internal
{
    internal sealed class ReflectionMethodCache
    {
        private readonly ConcurrentDictionary<Tuple<Type, string, BindingFlags>, MethodInfo> _methodCache =
            new ConcurrentDictionary<Tuple<Type, string, BindingFlags>, MethodInfo>();

        private readonly ConcurrentDictionary<Tuple<Type, string, Type, BindingFlags>, MethodInfo> _closedGenericMethodCache =
            new ConcurrentDictionary<Tuple<Type, string, Type, BindingFlags>, MethodInfo>();

        public MethodInfo GetMethod(Type ownerType, string methodName, BindingFlags bindingFlags)
        {
            var key = Tuple.Create(ownerType, methodName, bindingFlags);
            return _methodCache.GetOrAdd(key, _ => ownerType.GetMethod(methodName, bindingFlags));
        }

        public MethodInfo GetClosedGenericMethod(Type ownerType, string methodName, Type typeArgument, BindingFlags bindingFlags)
        {
            var key = Tuple.Create(ownerType, methodName, typeArgument, bindingFlags);
            return _closedGenericMethodCache.GetOrAdd(key, _ =>
                ownerType.GetMethod(methodName, bindingFlags).MakeGenericMethod(new[] { typeArgument }));
        }
    }
}