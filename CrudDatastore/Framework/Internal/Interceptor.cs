using System;
using System.Collections.Generic;
using System.Reflection;

namespace CrudDatastore.Framework.Internal
{
    internal class Interceptor : IIntercept
    {
        private readonly IDictionary<string, object> _props;
        private readonly Action<IDictionary<string, object>, object> _init;
        private readonly Func<IDictionary<string, object>, MethodInfo, object, object[], object> _intercept;

        public Interceptor(IDictionary<string, object> props, Action<IDictionary<string, object>, object> init, Func<IDictionary<string, object>, MethodInfo, object, object[], object> intercept)
        {
            _props = props;
            _init = init;
            _intercept = intercept;
        }

        public void Init(object proxy)
        {
            _init(_props, proxy);
        }

        public object Intercept(MethodInfo method, object proxy, params object[] args)
        {
            return _intercept(_props, method, proxy, args);
        }
    }
}
