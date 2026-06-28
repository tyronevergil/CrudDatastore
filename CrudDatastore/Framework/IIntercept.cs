using System;
using System.Reflection;

namespace CrudDatastore.Framework
{
    public interface IIntercept
    {
        void Init(object proxy);
        object Intercept(MethodInfo method, object proxy, params object[] args);
    }
}
