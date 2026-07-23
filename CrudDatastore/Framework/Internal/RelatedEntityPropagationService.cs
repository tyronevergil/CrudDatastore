using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace CrudDatastore.Framework.Internal
{
    internal sealed class RelatedEntityPropagationService
    {
        private readonly IDictionary<PropertyInfo, Delegate> _dataMapping;
        private readonly IDictionary<Type, IDataQuery> _dataQueries;

        public RelatedEntityPropagationService(
            IDictionary<PropertyInfo, Delegate> dataMapping,
            IDictionary<Type, IDataQuery> dataQueries)
        {
            _dataMapping = dataMapping;
            _dataQueries = dataQueries;
        }

        public Task PropagateAsync(
            IEnumerable<EntityEntry> entries,
            Func<object, Type> getEntityType,
            Func<object, object, Type, Delegate> createMarkNewDelegate)
        {
            foreach (var e in entries.Where(entry => entry.State == EntityEntry.States.New || entry.State == EntityEntry.States.Modified))
            {
                var entry = e.Entry;
                var entity = e.Entity;

                var entityType = getEntityType(entity);
                foreach (var prop in entityType.GetProperties().Where(p => typeof(EntityBase).IsAssignableFrom(p.PropertyType) || (p.PropertyType.IsGenericType && typeof(EntityBase).IsAssignableFrom(p.PropertyType.GetGenericArguments().First()))))
                {
                    var propValue = prop.GetValue(entity);
                    var propType = propValue != null ? propValue.GetType() : prop.PropertyType;
                    if (typeof(IObjectProxy).IsAssignableFrom(propType) || typeof(IEntityCollection).IsAssignableFrom(propType))
                        continue;

                    if (!_dataMapping.ContainsKey(prop))
                        continue;

                    var expressionPredicate = _dataMapping[prop].DynamicInvoke(entry, _dataQueries);

                    var relatedEntityType = prop.PropertyType.IsGenericType ? prop.PropertyType.GetGenericArguments().First() : prop.PropertyType;
                    if (!_dataQueries.ContainsKey(relatedEntityType))
                        continue;

                    var markNew = createMarkNewDelegate(entry, expressionPredicate, relatedEntityType);

                    if (prop.PropertyType.IsGenericType)
                    {
                        var entityPropCollection = propValue as System.Collections.IEnumerable;

                        if (entityPropCollection != null)
                        {
                            foreach (var item in entityPropCollection)
                            {
                                markNew.DynamicInvoke(item);
                            }
                        }
                    }
                    else
                    {
                        if (propValue != null)
                            markNew.DynamicInvoke(propValue);
                    }
                }
            }

            return Task.CompletedTask;
        }
    }
}