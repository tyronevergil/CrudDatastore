using CrudDatastore.Framework.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace CrudDatastore.Framework
{
    public abstract class QueryUnitBase : IQueryUnit
    {
        private bool _disposed;
        private bool _propDataQueriesAdded;

        private readonly object _sync = new object();

        private readonly QueryMaterializationService _materialization;

        protected readonly IDictionary<Type, IDataQuery> _dataQueries = new Dictionary<Type, IDataQuery>();

        protected readonly IDictionary<PropertyInfo, Delegate> _dataMapping = new Dictionary<PropertyInfo, Delegate>();
        protected readonly IDictionary<Tuple<Type, Type>, Type> _dataTableMapping = new Dictionary<Tuple<Type, Type>, Type>();

        protected readonly IDictionary<EntityBase, EntityBase> _materializedEntities = new Dictionary<EntityBase, EntityBase>();
        protected readonly IDictionary<EntityBase, IDictionary<string, object>> _materializedEntityProperties = new Dictionary<EntityBase, IDictionary<string, object>>();

        public event EventHandler<EntityEventArgs> EntityMaterialized;

        protected QueryUnitBase()
        {
            _materialization = new QueryMaterializationService(
                _dataQueries,
                _dataMapping,
                _materializedEntities,
                _materializedEntityProperties,
                (entity) => EntityMaterialized?.Invoke(this, new EntityEventArgs(entity)),
                (entity, resolveRelatedSynchronously) => CreateEntityProxyObjectInternal(entity, resolveRelatedSynchronously),
                (entity, expressionPredicate, relatedEntityType, relatedData) => CreateRelatedEntityCollection(entity, expressionPredicate, relatedEntityType, relatedData));
        }

        private Expression<Func<T, bool>> ModifierPredicate<T>(Expression<Func<T, bool>> predicate) where T : EntityBase
        {
            return _materialization.ModifierPredicate(predicate);
        }

        private T MaterializeEntityObject<T>(T entity) where T : EntityBase
        {
            return _materialization.MaterializeEntity(entity);
        }

        private async Task<T> MaterializeEntityObjectAsync<T>(T entity) where T : EntityBase
        {
            return await _materialization.MaterializeEntityAsync(entity).ConfigureAwait(false);
        }

        //private T CreateEntityCloneObject<T>(T entity) where T : EntityBase
        //{
        //    if (entity == null)
        //        return entity;

        //    var method = typeof(T).GetMethod(nameof(MemberwiseClone), BindingFlags.Instance | BindingFlags.NonPublic);
        //    return (T)method.Invoke(entity, null);
        //}

        private T CreateEntityProxyObject<T>(T entity, bool resolveRelatedSynchronously = true) where T : EntityBase
        {
            IDictionary<string, object> entityProps;
            if (!_materializedEntityProperties.TryGetValue(entity, out entityProps))
            {
                entityProps = new Dictionary<string, object>();
                _materializedEntityProperties.Add(entity, entityProps);
            }

            var entityType = typeof(T);
            var obj = Activator.CreateInstance(ProxyBuilder.CreateProxyType(entityType),
                new Interceptor(entityProps, 
                (props, proxy) =>
                {
                    foreach (var prop in entityType.GetProperties().Where(p => p.GetAccessors().Any(a => !(a.IsVirtual && !a.IsFinal))))
                    {
                        if (prop.GetSetMethod(false) != null)
                        {
                            object value = null;

                            value = prop.GetValue(entity, null);
                            if (value == null && resolveRelatedSynchronously)
                            {
                                value = GetRelatedPropertyValue(entity, prop);
                            }

                            if (value != null)
                                prop.SetValue(proxy, value, null);
                        }
                    }
                },
                (props, method, proxy, parameters) =>
                {
                    var propName = method.Name.Replace("set_", "").Replace("get_", "");

                    var propType = method.ReturnType;
                    if (propType == typeof(void))
                    {
                        if (!props.ContainsKey(propName))
                        {
                            props.Add(propName, parameters[0]);
                        }
                        else
                        {
                            props[propName] = parameters[0];
                        }
                    }
                    else
                    {
                        if (!props.ContainsKey(propName))
                        {
                            var prop = entity.GetType().GetProperty(propName);
                            var value = prop.GetValue(entity, null);
                            if (value == null)
                            {
                                if (propType.IsValueType)
                                {
                                    value = Activator.CreateInstance(propType);
                                }
                                else if (resolveRelatedSynchronously)
                                {
                                    value = GetRelatedPropertyValue(entity, prop);
                                }

                                if (value != null)
                                    proxy.GetType().GetProperty(propName).SetValue(proxy, value, null);
                            }

                            if (!props.ContainsKey(propName))
                            {
                                props.Add(propName, value);
                            }
                        }

                        return props[propName];
                    }

                    return null;
                }));

            return (T)obj;
        }

        private object CreateEntityProxyObjectInternal(object entity, bool resolveRelatedSynchronously)
        {
            var method = typeof(QueryUnitBase).GetMethod(nameof(CreateEntityProxyObject), BindingFlags.Instance | BindingFlags.NonPublic);
            var genericMethod = method.MakeGenericMethod(entity.GetType());
            return genericMethod.Invoke(this, new object[] { entity, resolveRelatedSynchronously });
        }

        protected async Task<object> GetRelatedPropertyValueAsync(object entity, PropertyInfo prop)
        {
            return await _materialization.GetRelatedPropertyValueAsync(entity, prop).ConfigureAwait(false);
        }

        // Sync wrapper for proxy initialization (blocks on async call)
        protected object GetRelatedPropertyValue(object entity, PropertyInfo prop)
        {
            return _materialization.GetRelatedPropertyValue(entity, prop);
        }

        protected virtual object CreateRelatedEntityCollection(object entity, object expressionPredicate, Type relatedEntityType, object relatedData)
        {
            var relatedEntityCollectionType = typeof(EntityCollection<>).MakeGenericType(new[] { relatedEntityType });
            var relatedEntityCollectionObject = Activator.CreateInstance(relatedEntityCollectionType, new[] { relatedData });

            return relatedEntityCollectionObject;
        }

        private void RegisterDataQuery(Type type, IDataQuery dataQuery)
        {
            if (type == null || dataQuery == null)
                throw new ArgumentNullException();

            /* see DataQuery SetMaterializationBehavior */
            var setMaterializationBehaviorMethod = dataQuery.GetType().GetMethod("SetMaterializationBehavior", BindingFlags.Instance | BindingFlags.NonPublic);
            if (setMaterializationBehaviorMethod != null)
            {
                var typeMaterializeEntityObject = typeof(QueryUnitBase).GetMethod(nameof(MaterializeEntityObject), BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(type);
                var delegateMaterializeObject = Delegate.CreateDelegate(typeof(Func<,>).MakeGenericType(type, type), this, typeMaterializeEntityObject);

                var typeModifierPredicate = typeof(QueryUnitBase).GetMethod(nameof(ModifierPredicate), BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(type);
                var typePredicate = typeof(Expression<>).MakeGenericType(typeof(Func<,>).MakeGenericType(type, typeof(bool)));
                var delegateModifierPredicate = Delegate.CreateDelegate(typeof(Func<,>).MakeGenericType(typePredicate, typePredicate), this, typeModifierPredicate);

                setMaterializationBehaviorMethod.Invoke(dataQuery, new object[] { delegateMaterializeObject, delegateModifierPredicate });
            }

            var setAsyncMaterializationBehaviorMethod = dataQuery.GetType().GetMethod("SetAsyncMaterializationBehavior", BindingFlags.Instance | BindingFlags.NonPublic);
            if (setAsyncMaterializationBehaviorMethod != null)
            {
                var typeMaterializeEntityObjectAsync = typeof(QueryUnitBase).GetMethod(nameof(MaterializeEntityObjectAsync), BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(type);
                var taskType = typeof(Task<>).MakeGenericType(type);
                var delegateMaterializeObjectAsync = Delegate.CreateDelegate(typeof(Func<,>).MakeGenericType(type, taskType), this, typeMaterializeEntityObjectAsync);

                setAsyncMaterializationBehaviorMethod.Invoke(dataQuery, new object[] { delegateMaterializeObjectAsync });
            }

                if (!_dataQueries.ContainsKey(type))
                {
                    _dataQueries.Add(type, dataQuery);
                }

                /*
                var typeDataQuery = typeof(MaterializeObjectDataQuery<>).MakeGenericType(type);
                var typeMaterializeEntityObject = typeof(QueryUnitBase).GetMethod(nameof(MaterializeEntityObject), BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(type);

                var delegateMaterializeObject = Delegate.CreateDelegate(typeof(Func<,>).MakeGenericType(type, type), this, typeMaterializeEntityObject);

                _dataQueries.Add(type, (IDataQuery)Activator.CreateInstance(typeDataQuery, new object[] { dataQuery, delegateMaterializeObject }));
                */
            }

            protected virtual IPropertyMap<T> Register<T>(IDataQuery<T> dataQuery) where T : EntityBase
            {
                RegisterDataQuery(typeof(T), dataQuery);

                return new PropertyMap<T>(_dataMapping, _dataTableMapping);
            }

        public virtual IDataQuery<T> Read<T>() where T : EntityBase
        {
            lock (_sync)
            {
                if (!_propDataQueriesAdded)
                {
                    _propDataQueriesAdded = true;
                    foreach (var prop in this.GetType()
                        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Where(p =>
                            (p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition().IsAssignableFrom(typeof(IDataQuery<>))) ||
                            (p.PropertyType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition().IsAssignableFrom(typeof(IDataQuery<>)))))
                    .ToList())
                    {
                        var t = prop.PropertyType.GetGenericArguments()[0];
                        if (!_dataQueries.ContainsKey(t))
                        {
                            var dq = prop.GetValue(this);
                            if (dq != null)
                                RegisterDataQuery(t, (IDataQuery)dq);
                        }
                    }
                }
            }

            var type = typeof(T);
            IDataQuery dataQuery;
            if (_dataQueries.TryGetValue(type, out dataQuery))
                return (IDataQuery<T>)dataQuery;

            return null;
        }

        public virtual void Execute(string command, params object[] parameters)
        {
        }

        public virtual Task ExecuteAsync(string command, params object[] parameters)
        {
            return Task.CompletedTask;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Free other state (managed objects)
                }

                // Free your own state
                foreach (var disposable in _dataQueries.Values)
                {
                    disposable.Dispose();
                }

                _dataQueries.Clear();
                _dataMapping.Clear();
                _dataTableMapping.Clear();
                _materializedEntities.Clear();
                _materializedEntityProperties.Clear();

                //
                _disposed = true;
            }
        }

        public virtual void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~QueryUnitBase()
        {
            Dispose(false);
        }
    }
}
