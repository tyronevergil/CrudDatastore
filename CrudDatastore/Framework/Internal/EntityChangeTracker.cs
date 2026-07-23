using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CrudDatastore.Framework.Internal
{
    internal sealed class EntityChangeTracker
    {
        private readonly IDictionary<EntityBase, EntityEntry> _entityEntries;
        private readonly IDictionary<EntityBase, EntityBase> _materializedEntities;

        public EntityChangeTracker(
            IDictionary<EntityBase, EntityEntry> entityEntries,
            IDictionary<EntityBase, EntityBase> materializedEntities)
        {
            _entityEntries = entityEntries;
            _materializedEntities = materializedEntities;
        }

        public IDictionary<EntityBase, EntityEntry> EntityEntries => _entityEntries;

        public void MarkEntityState<T>(T entity, EntityEntry.States state, Action<object> onCommit, Action<object> onCommitted) where T : EntityBase
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            EntityEntry existingEntityEntry;
            if (_entityEntries.TryGetValue(entity, out existingEntityEntry))
            {
                if (existingEntityEntry.State == EntityEntry.States.Deleted && state == EntityEntry.States.Modified)
                    return;

                if (existingEntityEntry.State == EntityEntry.States.New && state == EntityEntry.States.Modified)
                    return;

                existingEntityEntry.ChangeState(state);
                return;
            }

            var entry = _materializedEntities.Any(m => m.Value == entity) ? _materializedEntities.FirstOrDefault(m => m.Value == entity).Key : entity;
            _entityEntries.Add(entity, new EntityEntry(state, entry, entity, onCommit, onCommitted));
        }

        public void DetectMaterializedEntityChanges(Action<object> markModified)
        {
            var markModifiedAction = markModified ?? (_ => { });

            foreach (var item in _materializedEntities.ToList())
            {
                var entry = item.Key;
                var entity = item.Value;

                if (entry.GetType().GetProperties().Where(prop => prop.PropertyType.IsValueType || Type.GetTypeCode(prop.PropertyType) == TypeCode.String)
                        .Any(prop => !Equals(prop.GetValue(entry, null), prop.GetValue(entity, null))))
                {
                    markModifiedAction(entity);
                }

                if (entity.GetType().GetProperties().Where(p => p.GetAccessors().Any(a => a.IsVirtual && !a.IsFinal))
                        .Any(prop =>
                        {
                            var value = prop.GetValue(entity, null);
                            if (value != null)
                            {
                                var propType = value.GetType();
                                return !(typeof(IObjectProxy).IsAssignableFrom(propType) || typeof(IEntityCollection).IsAssignableFrom(propType));
                            }

                            return false;
                        }))
                {
                    markModifiedAction(entity);
                }
            }
        }
    }
}