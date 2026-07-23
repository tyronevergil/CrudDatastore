using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace CrudDatastore.Framework.Internal
{
    internal sealed class QueryMaterializationService
    {
        private readonly IDictionary<Type, IDataQuery> _dataQueries;
        private readonly IDictionary<PropertyInfo, Delegate> _dataMapping;
        private readonly IDictionary<EntityBase, EntityBase> _materializedEntities;
        private readonly IDictionary<EntityBase, IDictionary<string, object>> _materializedEntityProperties;
        private readonly Action<EntityBase> _materializedCallback;
        private readonly Func<object, bool, object> _createEntityProxyObject;
        private readonly Func<object, object, Type, object, object> _createRelatedEntityCollection;

        public QueryMaterializationService(
            IDictionary<Type, IDataQuery> dataQueries,
            IDictionary<PropertyInfo, Delegate> dataMapping,
            IDictionary<EntityBase, EntityBase> materializedEntities,
            IDictionary<EntityBase, IDictionary<string, object>> materializedEntityProperties,
            Action<EntityBase> materializedCallback,
            Func<object, bool, object> createEntityProxyObject,
            Func<object, object, Type, object, object> createRelatedEntityCollection)
        {
            _dataQueries = dataQueries;
            _dataMapping = dataMapping;
            _materializedEntities = materializedEntities;
            _materializedEntityProperties = materializedEntityProperties;
            _materializedCallback = materializedCallback;
            _createEntityProxyObject = createEntityProxyObject;
            _createRelatedEntityCollection = createRelatedEntityCollection;
        }

        public Expression<Func<T, bool>> ModifierPredicate<T>(Expression<Func<T, bool>> predicate) where T : EntityBase
        {
            return (Expression<Func<T, bool>>)InterceptNavigationPropertyExpressionTreeModifier.CopyAndModify(
                predicate,
                (entity, prop) => GetRelatedPropertyValue(entity, entity.GetType().GetProperty(prop)));
        }

        public T MaterializeEntity<T>(T entity) where T : EntityBase
        {
            if (entity == null)
                return entity;

            if (typeof(IObjectProxy).IsAssignableFrom(entity.GetType()))
                return entity;

            if (_materializedEntities.ContainsKey(entity))
                return (T)_materializedEntities[entity];

            var entityObject = (T)_createEntityProxyObject(entity, true);

            _materializedEntities.Add(entity, entityObject);

            if (_materializedCallback != null)
                _materializedCallback(entityObject);

            return entityObject;
        }

        public async Task<T> MaterializeEntityAsync<T>(T entity) where T : EntityBase
        {
            if (entity == null)
                return entity;

            if (typeof(IObjectProxy).IsAssignableFrom(entity.GetType()))
                return entity;

            if (_materializedEntities.ContainsKey(entity))
                return (T)_materializedEntities[entity];

            var entityObject = (T)_createEntityProxyObject(entity, false);

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

            if (_materializedCallback != null)
                _materializedCallback(entityObject);

            return entityObject;
        }

        public object GetRelatedPropertyValue(object entity, PropertyInfo prop)
        {
            return GetRelatedPropertyValueAsync(entity, prop).GetAwaiter().GetResult();
        }

        public async Task<object> GetRelatedPropertyValueAsync(object entity, PropertyInfo prop)
        {
            object value = null;

            if (prop != null && _dataMapping.ContainsKey(prop))
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

                    var findAsyncMethod = ds.GetType().GetMethod("FindAsync");
                    var task = (Task)findAsyncMethod.Invoke(ds, new[] { specObject });
                    var relatedData = await GetTaskResultAsync(task).ConfigureAwait(false);
                    relatedData = MaterializeQueryableData(relatedEntityType, relatedData);

                    if (isGenericType)
                    {
                        value = _createRelatedEntityCollection(entity, expressionPredicate, relatedEntityType, relatedData);
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
    }
}