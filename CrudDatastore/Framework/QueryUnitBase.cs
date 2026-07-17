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

        protected readonly IDictionary<Type, IDataQuery> _dataQueries = new Dictionary<Type, IDataQuery>();

        protected readonly IDictionary<PropertyInfo, Delegate> _dataMapping = new Dictionary<PropertyInfo, Delegate>();
        protected readonly IDictionary<Tuple<Type, Type>, Type> _dataTableMapping = new Dictionary<Tuple<Type, Type>, Type>();

        protected readonly IDictionary<EntityBase, EntityBase> _materializedEntities = new Dictionary<EntityBase, EntityBase>();
        protected readonly IDictionary<EntityBase, IDictionary<string, object>> _materializedEntityProperties = new Dictionary<EntityBase, IDictionary<string, object>>();

        public event EventHandler<EntityEventArgs> EntityMaterialized;

        protected QueryUnitBase()
        {
        }

        private Expression<Func<T, bool>> ModifierPredicate<T>(Expression<Func<T, bool>> predicate) where T : EntityBase
        {
            var modifiedPredicate = (Expression<Func<T, bool>>)InterceptNavigationPropertyExpressionTreeModifier.CopyAndModify(predicate, (entity, prop) => GetRelatedPropertyValue(entity, entity.GetType().GetProperty(prop)));
            return modifiedPredicate;
        }

        private static object GetTaskResult(Task task)
        {
            task.GetAwaiter().GetResult();

            var resultProperty = task.GetType().GetProperty("Result", BindingFlags.Public | BindingFlags.Instance);
            if (resultProperty == null)
                return null;

            return resultProperty.GetValue(task);
        }

        private static async Task<object> GetTaskResultAsync(Task task)
        {
            await task.ConfigureAwait(false);

            var resultProperty = task.GetType().GetProperty("Result", BindingFlags.Public | BindingFlags.Instance);
            if (resultProperty == null)
                return null;

            return resultProperty.GetValue(task);
        }

        private object MaterializeQueryableData(Type entityType, object queryableData)
        {
            var toListMethod = typeof(Enumerable)
                .GetGenericMethod("ToList", new[] { typeof(IEnumerable<>) })
                .MakeGenericMethod(new[] { entityType });

            var list = toListMethod.Invoke(null, new[] { queryableData });

            var asQueryableMethod = typeof(Queryable)
                .GetGenericMethod("AsQueryable", new[] { typeof(IEnumerable<>) })
                .MakeGenericMethod(new[] { entityType });

            return asQueryableMethod.Invoke(null, new[] { list });
        }

        private T MaterializeEntityObject<T>(T entity) where T : EntityBase
        {
            if (entity == null)
                return entity;

            if (typeof(IObjectProxy).IsAssignableFrom(entity.GetType()))
                return entity;

            if (_materializedEntities.ContainsKey(entity))
                return (T)_materializedEntities[entity];

            var entityObject = CreateEntityProxyObject(entity);

            _materializedEntities.Add(entity, entityObject);

            EntityMaterialized?.Invoke(this, new EntityEventArgs(entityObject));

            return entityObject;
        }

        private async Task<T> MaterializeEntityObjectAsync<T>(T entity) where T : EntityBase
        {
            if (entity == null)
                return entity;

            if (typeof(IObjectProxy).IsAssignableFrom(entity.GetType()))
                return entity;

            if (_materializedEntities.ContainsKey(entity))
                return (T)_materializedEntities[entity];

            var entityObject = CreateEntityProxyObject(entity, false);

            _materializedEntities.Add(entity, entityObject);

            var entityType = typeof(T);
            foreach (var prop in entityType.GetProperties().Where(p => p.GetSetMethod(false) != null))
            {
                object value = prop.GetValue(entity, null);
                if (value == null)
                {
                    value = await GetRelatedPropertyValueAsync(entity, prop).ConfigureAwait(false);
                }

                if (value != null)
                    prop.SetValue(entityObject, value, null);
            }

            EntityMaterialized?.Invoke(this, new EntityEventArgs(entityObject));

            return entityObject;
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

        protected async Task<object> GetRelatedPropertyValueAsync(object entity, PropertyInfo prop)
        {
            object value = null;

            if (_dataMapping.ContainsKey(prop))
            {
                var expressionPredicate = _dataMapping[prop].DynamicInvoke(entity, _dataQueries);

                var propType = prop.PropertyType;
                var isGenericType = propType.IsGenericType;

                var relatedEntityType = isGenericType ? propType.GetGenericArguments().First() : propType;
                if (_dataQueries.ContainsKey(relatedEntityType))
                {
                    var ds = _dataQueries[relatedEntityType];

                    var specType = typeof(Specification<>).MakeGenericType(new[] { relatedEntityType });
                    var specObject = Activator.CreateInstance(specType, new[] { expressionPredicate });

                    // Always use async API (single code path)
                    var findAsyncMethod = ds.GetType().GetMethod("FindAsync");
                    var task = (Task)findAsyncMethod.Invoke(ds, new[] { specObject });
                    var relatedData = await GetTaskResultAsync(task).ConfigureAwait(false);
                    relatedData = MaterializeQueryableData(relatedEntityType, relatedData);

                    if (isGenericType)
                    {
                        value = CreateRelatedEntityCollection(entity, expressionPredicate, relatedEntityType, relatedData);
                    }
                    else
                    {
                        var firstOrDefaultMethod = typeof(Queryable)
                                .GetGenericMethod("FirstOrDefault", new[] { typeof(IQueryable<>) })
                                .MakeGenericMethod(new Type[] { relatedEntityType });
                        var firstEntityObject = firstOrDefaultMethod.Invoke(null, new[] { relatedData });

                        value = firstEntityObject;
                    }
                }
            }

            return value;
        }

        // Sync wrapper for proxy initialization (blocks on async call)
        protected object GetRelatedPropertyValue(object entity, PropertyInfo prop)
        {
            return GetRelatedPropertyValueAsync(entity, prop).GetAwaiter().GetResult();
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
