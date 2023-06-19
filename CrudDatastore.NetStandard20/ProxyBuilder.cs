using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

namespace CrudDatastore
{
    public interface IEntityProxy
    {
    }

    public interface IIntercept
    {
        object Intercept(MethodInfo method, object proxy, params object[] args);
    }

    internal class Interceptor : IIntercept
    {
        private readonly IDictionary<string, object> _props;
        private readonly Func<IDictionary<string, object>, MethodInfo, object, object[], object> _intercept;

        public Interceptor(IDictionary<string, object> props, Func<IDictionary<string, object>, MethodInfo, object, object[], object> intercept)
        {
            _props = props;
            _intercept = intercept;
        }

        public object Intercept(MethodInfo method, object proxy, params object[] args)
        {
            return _intercept(_props, method, proxy, args);
        }
    }

    //internal class Interceptor : IIntercept
    //{
    //    private readonly Func<Func<object>, MethodInfo, object, object[], object> _intercept;

    //    public Interceptor(Func<Func<object>, MethodInfo, object, object[], object> intercept)
    //    {
    //        _intercept = intercept;
    //    }

    //    public object Intercept(MethodInfo method, object proxy, params object[] args)
    //    {
    //        return _intercept(() => ExecuteBaseMethod(method, proxy, args), method, proxy, args);
    //    }

    //    protected static object ExecuteBaseMethod(MethodInfo method, object proxy, params object[] args)
    //    {
    //        var proxyType = proxy.GetType();

    //        var parameterInfos = method.GetParameters();

    //        var returnType = method.ReturnType;
    //        var parameterTypes = (new[] { method.ReflectedType }).Concat(parameterInfos.Select(p => p.ParameterType)).ToArray();

    //        var dm = new DynamicMethod("__base", returnType, parameterTypes, proxyType);

    //        // generate IL instructions
    //        ILGenerator gen = dm.GetILGenerator();
    //        gen.Emit(OpCodes.Ldarg_0);

    //        foreach (var p in parameterInfos)
    //        {
    //            gen.Emit(OpCodes.Ldarg_S, p.Position + 1);
    //        }

    //        gen.Emit(OpCodes.Call, method);
    //        gen.Emit(OpCodes.Ret);

    //        // create delegate to invoke the base method
    //        var delMethod = dm.CreateDelegate(Expression.GetDelegateType(parameterTypes.Concat(new[] { returnType }).ToArray()));
    //        var delArgs = (new[] { proxy }).Concat(args ?? Enumerable.Empty<object>()).ToArray();
    //        var delRet = delMethod.DynamicInvoke(delArgs);

    //        return delRet;
    //    }
    //}

    internal sealed class ProxyBuilder
    {
        private static readonly ModuleBuilder ModuleBuilder;
        private static readonly IDictionary<string, Type> ProxyTypeCaches;

        static ProxyBuilder()
        {
            var assemblyBuilderAccess = AssemblyBuilderAccess.RunAndCollect;

            var assemblyName = new AssemblyName("CrudDatastore.DynamicProxies");
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, assemblyBuilderAccess);

            ModuleBuilder = assemblyBuilder.DefineDynamicModule("CrudDatastore.DynamicProxies");

            ProxyTypeCaches = new ConcurrentDictionary<string, Type>();
        }

        public static Type CreateProxyType(Type targetType)
        {
            var proxyTypeName = string.Format("{0}.{1}_Proxy", ModuleBuilder.ScopeName, targetType.FullName.Replace('.', '_'));

            lock (ProxyTypeCaches)
            {
                if (!ProxyTypeCaches.ContainsKey(proxyTypeName))
                {
                    TypeBuilder tb = ModuleBuilder.DefineType(
                        proxyTypeName,
                        TypeAttributes.Public | TypeAttributes.Class,
                        targetType,
                        new Type[] { typeof(IEntityProxy) });

                    // interceptor
                    FieldBuilder interceptor = tb.DefineField(
                                "__interceptor",
                                typeof(IIntercept),
                                FieldAttributes.Private);

                    // constructor
                    ConstructorInfo ctorInfo = typeof(object).GetConstructor(Type.EmptyTypes);

                    ConstructorBuilder ctor = tb.DefineConstructor(
                                MethodAttributes.Public |
                                MethodAttributes.HideBySig |
                                MethodAttributes.SpecialName |
                                MethodAttributes.RTSpecialName,
                                CallingConventions.Standard,
                                new Type[] { typeof(IIntercept) });

                    ILGenerator ctorIL = ctor.GetILGenerator();

                    ctorIL.Emit(OpCodes.Ldarg_0);
                    ctorIL.Emit(OpCodes.Call, ctorInfo);

                    ctorIL.Emit(OpCodes.Ldarg_0);
                    ctorIL.Emit(OpCodes.Ldarg_1);
                    ctorIL.Emit(OpCodes.Stfld, interceptor);

                    ctorIL.Emit(OpCodes.Ret);

                    // properties
                    MethodInfo interceptMethodInfo = typeof(IIntercept).GetMethod("Intercept", new Type[] { typeof(MethodInfo), typeof(object), typeof(object[]) });
                    MethodInfo getMethodFromHandleMethodInfo = typeof(MethodBase).GetMethod("GetMethodFromHandle", new Type[] { typeof(RuntimeMethodHandle) });

                    foreach (PropertyInfo p in targetType.GetProperties().Where(p => p.GetAccessors().Any(a => a.IsVirtual && !a.IsFinal)))
                    {
                        PropertyBuilder pb = tb.DefineProperty(p.Name, PropertyAttributes.None, p.PropertyType, Type.EmptyTypes);

                        foreach (MethodInfo m in p.GetAccessors().Where(a => !a.IsFinal))
                        {
                            ParameterInfo[] parameters = m.GetParameters();
                            List<Type> t = parameters.Select(param => param.ParameterType).ToList();

                            MethodBuilder mb = tb.DefineMethod(
                                    m.Name,
                                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.SpecialName,
                                    CallingConventions.HasThis,
                                    m.ReturnType,
                                    t.ToArray());

                            mb.SetImplementationFlags(MethodImplAttributes.Managed | MethodImplAttributes.IL);

                            //
                            ILGenerator mbIL = mb.GetILGenerator();

                            mbIL.Emit(OpCodes.Ldarg_0);

                            mbIL.Emit(OpCodes.Ldfld, interceptor);

                            mbIL.Emit(OpCodes.Ldtoken, m);
                            mbIL.Emit(OpCodes.Call, getMethodFromHandleMethodInfo);

                            mbIL.Emit(OpCodes.Ldarg_0);

                            LocalBuilder args = mbIL.DeclareLocal(typeof(object[]));
                            if (parameters.Length > 0)
                            {
                                mbIL.Emit(OpCodes.Ldc_I4_S, parameters.Length);
                                mbIL.Emit(OpCodes.Newarr, typeof(object));

                                mbIL.Emit(OpCodes.Stloc, args);
                                mbIL.Emit(OpCodes.Ldloc, args);

                                for (int i = 0; i < parameters.Length; i++)
                                {
                                    mbIL.Emit(OpCodes.Ldc_I4_S, i);
                                    mbIL.Emit(OpCodes.Ldarg_S, i + 1);

                                    if (t[i].IsValueType || t[i].IsGenericParameter)
                                        mbIL.Emit(OpCodes.Box, t[i]);

                                    if (t[i].IsByRef)
                                        mbIL.Emit(OpCodes.Ldind_Ref);

                                    mbIL.Emit(OpCodes.Stelem_Ref);
                                    mbIL.Emit(OpCodes.Ldloc, args);
                                }
                            }
                            else
                            {
                                mbIL.Emit(OpCodes.Ldnull);
                            }

                            mbIL.Emit(OpCodes.Callvirt, interceptMethodInfo);

                            if (m.ReturnType == typeof(void))
                            {
                                mbIL.Emit(OpCodes.Pop);
                            }
                            else
                            {
                                mbIL.DeclareLocal(m.ReturnType);
                                mbIL.Emit(OpCodes.Unbox_Any, m.ReturnType);
                            }

                            mbIL.Emit(OpCodes.Ret);

                            if (m.ReturnType == typeof(void))
                            {
                                pb.SetSetMethod(mb);
                            }
                            else
                            {
                                pb.SetGetMethod(mb);
                            }

                        }
                    }

                    ProxyTypeCaches.Add(proxyTypeName, tb.CreateTypeInfo());
                }

                return ProxyTypeCaches[proxyTypeName];
            }
        }
    }
}
