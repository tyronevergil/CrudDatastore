using CrudDatastore.Foundation;
using CrudDatastore.Framework.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace CrudDatastore.Framework
{
    public abstract class UnitOfWorkBase : QueryUnitBase, IUnitOfWork
    {
        private bool _disposed;

        protected UnitOfWorkBase()
        {
        }

        private readonly IDictionary<Type, IDataStore> _dataStores = new Dictionary<Type, IDataStore>();

        private readonly Dictionary<EntityBase, EntityEntry> _entityEntries = new Dictionary<EntityBase, EntityEntry>();

        public event EventHandler<EntityEventArgs> EntityCreate;
        public event EventHandler<EntityEventArgs> EntityUpdate;
        public event EventHandler<EntityEventArgs> EntityDelete;

        public virtual void MarkNew<T>(T entity) where T : EntityBase
        {
            MarkEntityState(entity, EntityEntry.States.New , (e) => { }, (e) => { });
        }

        public virtual Task MarkNewAsync<T>(T entity) where T : EntityBase
        {
            MarkNew(entity);

            return Task.CompletedTask;
        }

        public virtual void MarkModified<T>(T entity) where T : EntityBase
        {
            MarkEntityState(entity, EntityEntry.States.Modified, (e) => { }, (e) => { });
        }

        public virtual Task MarkModifiedAsync<T>(T entity) where T : EntityBase
        {
            MarkModified(entity);

            return Task.CompletedTask;
        }

        public virtual void MarkDeleted<T>(T entity) where T : EntityBase
        {
            MarkEntityState(entity, EntityEntry.States.Deleted, (e) => { }, (e) => { });
        }

        public virtual Task MarkDeletedAsync<T>(T entity) where T : EntityBase
        {
            MarkDeleted(entity);

            return Task.CompletedTask;
        }

        public virtual void Commit()
        {
            DetectChanges();

            while (true)
            {
                var entry = _entityEntries.Where(e => e.Value.UnCommited).Select(e => e.Value).OrderBy(e => e.State).FirstOrDefault();
                if (entry != null)
                {

                    var b = true;
                    switch (entry.State)
                    {
                        case EntityEntry.States.New:
                            b = InvokeCreate(entry);
                            break;
                        case EntityEntry.States.Modified:
                            b = InvokeUpdate(entry);
                            break;
                        case EntityEntry.States.Deleted:
                            b = InvokeDelete(entry);
                            break;
                    }

                    if (b)
                        entry.Commit();
                }
                else
                    break;
            }

            _entityEntries.Clear();
            _materializedEntities.Clear();
        }

        public virtual async Task CommitAsync()
        {
            await DetectChangesAsync();

            while (true)
            {
                var entry = _entityEntries.Where(e => e.Value.UnCommited).Select(e => e.Value).OrderBy(e => e.State).FirstOrDefault();
                if (entry != null)
                {

                    var b = true;
                    switch (entry.State)
                    {
                        case EntityEntry.States.New:
                            b = await InvokeCreateAsync(entry);
                            break;
                        case EntityEntry.States.Modified:
                            b = await InvokeUpdateAsync(entry);
                            break;
                        case EntityEntry.States.Deleted:
                            b = await InvokeDeleteAsync(entry);
                            break;
                    }

                    if (b)
                        entry.Commit();
                }
                else
                    break;
            }

            _entityEntries.Clear();
            _materializedEntities.Clear();
        }

        protected virtual IPropertyMap<T> Register<T>(IDataStore<T> dataStore) where T : EntityBase
        {
            return base.Register(dataStore);
        }

        protected override object CreateRelatedEntityCollection(object entity, object expressionPredicate, Type relatedEntityType, object relatedData)
        {
            var unitOfWorkType = typeof(UnitOfWorkBase);

            var paramMarkNew = unitOfWorkType
                .GetMethod(nameof(MarkNewOnCommitUpdateProperties), BindingFlags.NonPublic | BindingFlags.Instance).MakeGenericMethod(new[] { relatedEntityType })
                .Invoke(this, new[] { entity, expressionPredicate });

            var paramMarkDeleted = unitOfWorkType
                .GetMethod(nameof(MarkDeletedOnCommitForDeletion), BindingFlags.NonPublic | BindingFlags.Instance).MakeGenericMethod(new[] { relatedEntityType })
                .Invoke(this, new object[] { entity });

            var relatedEntityCollectionType = typeof(EntityCollection<>).MakeGenericType(new[] { relatedEntityType });
            var relatedEntityCollectionObject = Activator.CreateInstance(relatedEntityCollectionType, new[] { relatedData, paramMarkNew, paramMarkDeleted });

            return relatedEntityCollectionObject;
        }

        private async Task DetectChangesAsync()
        {
            var markModified = typeof(UnitOfWorkBase).GetMethod(nameof(MarkModified));
            foreach (var item in _materializedEntities.ToList())
            {
                var entry = item.Key;
                var entity = item.Value;

                // quick path: value-type or string properties changed
                if (entry.GetType().GetProperties().Where(prop => prop.PropertyType.IsValueType || Type.GetTypeCode(prop.PropertyType) == TypeCode.String)
                        .Any(prop => !Equals(prop.GetValue(entry, null), prop.GetValue(entity, null))))
                {
                    markModified.MakeGenericMethod(new[] { entry.GetType() }).Invoke(this, new[] { entity });
                }

                // virtual navigation/property proxies: when a property on entity is non-null but not a proxy/collection -> treat as modification
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
                    markModified.MakeGenericMethod(new[] { entry.GetType() }).Invoke(this, new[] { entity });
                }
            }

            // process new/modified entries and ensure related entities are marked appropriately
            var entryList = _entityEntries.Values.Where(e => e.State == EntityEntry.States.New || e.State == EntityEntry.States.Modified).ToList();
            foreach (var e in entryList)
            {
                var entry = e.Entry;
                var entity = e.Entity;

                var entityType = GetEntityType(entity);
                foreach (var prop in entityType.GetProperties().Where(p => typeof(EntityBase).IsAssignableFrom(p.PropertyType) || (p.PropertyType.IsGenericType && typeof(EntityBase).IsAssignableFrom(p.PropertyType.GetGenericArguments().First()))))
                {
                    var propValue = prop.GetValue(entity);
                    var propType = propValue != null ? propValue.GetType() : prop.PropertyType;
                    if (!(typeof(IObjectProxy).IsAssignableFrom(propType) || typeof(IEntityCollection).IsAssignableFrom(propType)))
                    {
                        if (_dataMapping.ContainsKey(prop))
                        {
                            var expressionPredicate = _dataMapping[prop].DynamicInvoke(entry, _dataQueries);

                            var relatedEntityType = prop.PropertyType.IsGenericType ? prop.PropertyType.GetGenericArguments().First() : prop.PropertyType;
                            if (_dataQueries.ContainsKey(relatedEntityType))
                            {
                                var unitOfWorkType = typeof(UnitOfWorkBase);
                                var markNew = (Delegate)unitOfWorkType
                                    .GetMethod(nameof(MarkNewOnCommitUpdateProperties), BindingFlags.NonPublic | BindingFlags.Instance).MakeGenericMethod(new[] { relatedEntityType })
                                    .Invoke(this, new[] { entry, expressionPredicate });

                                if (prop.PropertyType.IsGenericType)
                                {
                                    var entityPropCollection = propValue as IEnumerable;
                                    var entryPropCollection = (prop.GetValue(entry) ?? await GetRelatedPropertyValueAsync(entry, prop)) as IEnumerable;

                                    if (entryPropCollection != null && (entityPropCollection == null || (entityPropCollection != null && entityPropCollection != entryPropCollection)))
                                    {
                                        //var markDeleted = typeof(UnitOfWorkBase).GetMethod(nameof(MarkDeleted));
                                        //foreach (var item in entryPropCollection)
                                        //{
                                        //    markDeleted.MakeGenericMethod(new[] { relatedEntityType }).Invoke(this, new[] { item });
                                        //}
                                    }

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
                    }
                }
            }
        }

        // Sync wrapper for DetectChanges calls
        private void DetectChanges()
        {
            DetectChangesAsync().GetAwaiter().GetResult();
        }

        private void MarkEntityState<T>(T entity, EntityEntry.States state, Action<object> onCommit, Action<object> onCommitted) where T : EntityBase
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            // try to find an existing registration directly by reference
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

        private Action<T> MarkNewOnCommitUpdateProperties<T>(object parent, Expression expression) where T : EntityBase
        {
            var properties = ConstantPropertiesVisitor<T>.GetProperties(expression);

            return (e) =>
            {
                MarkEntityState(e, EntityEntry.States.New,

                /* on commit */
                (obj) =>
                {
                    var parentType = GetEntityType(parent);
                    var entityType = typeof(T);

                    foreach (var p in properties)
                    {
                        var value = p.Value["value"];

                        var parentProp = parentType.GetProperty(p.Key);
                        if (parentProp != null)
                        {
                            value = parentProp.GetValue(parent);
                        }

                        var memberProp = entityType.GetProperty(p.Value["comparison"].ToString());
                        memberProp.SetValue(obj, value, null);
                    }

                    var keyTypes = Tuple.Create(parentType, entityType);
                    if (_dataTableMapping.ContainsKey(keyTypes))
                    {
                        if (typeof(IObjectProxy).IsAssignableFrom(obj.GetType()))
                        {
                            var mappingEntityType = _dataTableMapping[keyTypes];
                            var table = _dataQueries[mappingEntityType];

                            var mappingEntity = Activator.CreateInstance(mappingEntityType);
                            foreach (var p in mappingEntityType.GetProperties())
                            {
                                var value = default(object);

                                var entryProp = parentType.GetProperty(p.Name);
                                if (entryProp != null)
                                {
                                    value = entryProp.GetValue(parent);
                                }

                                var entityProp = entityType.GetProperty(p.Name);
                                if (entityProp != null)
                                {
                                    value = entityProp.GetValue(obj);
                                }

                                p.SetValue(mappingEntity, value, null);
                            }

                            var add = table.GetType().GetMethod("Add");
                            add.Invoke(table, new[] { mappingEntity });

                            MarkModified((T)obj);
                        }
                    }
                },

                /* committed */
                (obj) =>
                {
                    var parentType = GetEntityType(parent);
                    var entityType = typeof(T);

                    var keyTypes = Tuple.Create(parentType, entityType);
                    if (_dataTableMapping.ContainsKey(keyTypes))
                    {
                        var mappingEntityType = _dataTableMapping[keyTypes];
                        var table = _dataQueries[mappingEntityType];

                        var mappingEntity = Activator.CreateInstance(mappingEntityType);
                        foreach (var p in mappingEntityType.GetProperties())
                        {
                            var value = default(object);

                            var entryProp = parentType.GetProperty(p.Name);
                            if (entryProp != null)
                            {
                                value = entryProp.GetValue(parent);
                            }

                            var entityProp = entityType.GetProperty(p.Name);
                            if (entityProp != null)
                            {
                                value = entityProp.GetValue(obj);
                            }

                            p.SetValue(mappingEntity, value, null);
                        }

                        var add = table.GetType().GetMethod("Add");
                        add.Invoke(table, new[] { mappingEntity });
                    }

                    //
                    foreach (var prop in entityType.GetProperties().Where(p => p.GetAccessors().Any(a => (a.IsVirtual && !a.IsFinal))))
                    {
                        if (prop.GetSetMethod(false) != null)
                        {
                            object value = null;

                            value = prop.GetValue(obj, null);
                            if (value == null)
                            {
                                value = GetRelatedPropertyValue(obj, prop);
                            }

                            if (value != null)
                                prop.SetValue(obj, value, null);
                        }
                    }

                });
            };
        }

        private Action<T> MarkDeletedOnCommitForDeletion<T>(object parent) where T : EntityBase
        {
            return (e) =>
            {
                MarkEntityState(e, EntityEntry.States.Deleted,

                /* on commit */
                (obj) =>
                {
                    var parentType = GetEntityType(parent);
                    var entityType = typeof(T);

                    var keyTypes = Tuple.Create(parentType, entityType);
                    if (_dataTableMapping.ContainsKey(keyTypes))
                    {
                        var mappingEntityType = _dataTableMapping[keyTypes];
                        var table = _dataQueries[mappingEntityType];

                        var mappingEntity = Activator.CreateInstance(mappingEntityType);
                        foreach (var p in mappingEntityType.GetProperties())
                        {
                            var value = default(object);

                            var entryProp = parentType.GetProperty(p.Name);
                            if (entryProp != null)
                            {
                                value = entryProp.GetValue(parent);
                            }

                            var entityProp = entityType.GetProperty(p.Name);
                            if (entityProp != null)
                            {
                                value = entityProp.GetValue(obj);
                            }

                            p.SetValue(mappingEntity, value, null);
                        }

                        var delete = table.GetType().GetMethod("Delete");
                        delete.Invoke(table, new[] { mappingEntity });


                        // find
                        var param = Expression.Parameter(mappingEntityType, "e");
                        var expression = default(Expression);
                        foreach (var p in mappingEntityType.GetProperties())
                        {
                            var entityProp = entityType.GetProperty(p.Name);
                            if (entityProp != null)
                            {
                                var propExpression = Expression.Equal(Expression.Property(param, entityProp.Name), Expression.Constant(entityProp.GetValue(obj)));
                                if (expression != null)
                                    expression = Expression.AndAlso(expression, propExpression);
                                else
                                    expression = propExpression;
                            }
                        }

                        var lambdaMethod = typeof(Expression)
                                    .GetGenericMethod("Lambda", new[] { typeof(Expression), typeof(ParameterExpression[]) })
                                    .MakeGenericMethod(new[] { typeof(Func<,>).MakeGenericType(new[] { mappingEntityType, typeof(bool) }) });
                        var expressionPredicate = (LambdaExpression)lambdaMethod.Invoke(null, new object[] { expression, new[] { param } });

                        var specType = typeof(Specification<>).MakeGenericType(new[] { mappingEntityType });
                        var specObject = Activator.CreateInstance(specType, new[] { expressionPredicate });

                        var find = table.GetType().GetMethod("Find");
                        var data = find.Invoke(table, new[] { specObject });

                        var any = typeof(Enumerable)
                                    .GetGenericMethod("Any", new[] { typeof(IEnumerable<>) })
                                    .MakeGenericMethod(new Type[] { mappingEntityType });

                        var isAny = (bool)any.Invoke(null, new[] { data });
                        if (isAny)
                            MarkModified((T)obj);
                    }
                },

                /* committed */
                (obj) =>
                {


                });
            };
        }

        private bool InvokeCreate(EntityEntry entry)
        {
            var entity = entry.Entity;
            var entityType = GetEntityType(entity);
            if (_dataQueries.ContainsKey(entityType))
            {
                EntityCreate?.Invoke(this, new EntityEventArgs(entity));

                if (entry.State == EntityEntry.States.New)
                {
                    entry.OnCommit(entity);

                    if (entry.State == EntityEntry.States.New)
                    {
                        var ds = _dataQueries[entityType];
                        ds.GetType().GetMethod("Add").Invoke(ds, new[] { entity });

                        entry.OnCommitted(entity);
                    }
                    else
                        return false;
                }
                else
                    return false;
            }

            return true;
        }

        private async Task<bool> InvokeCreateAsync(EntityEntry entry)
        {
            var entity = entry.Entity;
            var entityType = GetEntityType(entity);
            if (_dataQueries.ContainsKey(entityType))
            {
                EntityCreate?.Invoke(this, new EntityEventArgs(entity));

                if (entry.State == EntityEntry.States.New)
                {
                    entry.OnCommit(entity);

                    if (entry.State == EntityEntry.States.New)
                    {
                        var ds = _dataQueries[entityType];
                        await (Task)ds.GetType().GetMethod("AddAsync").Invoke(ds, new[] { entity });

                        entry.OnCommitted(entity);
                    }
                    else
                        return false;
                }
                else
                    return false;
            }

            return true;
        }

        private bool InvokeUpdate(EntityEntry entry)
        {
            var entity = entry.Entity;
            var entityType = GetEntityType(entity);
            if (_dataQueries.ContainsKey(entityType))
            {
                EntityUpdate?.Invoke(this, new EntityEventArgs(entity));

                if (entry.State == EntityEntry.States.Modified)
                {
                    entry.OnCommit(entity);

                    if (entry.State == EntityEntry.States.Modified)
                    {
                        var ds = _dataQueries[entityType];
                        ds.GetType().GetMethod("Update").Invoke(ds, new[] { entity });

                        entry.OnCommitted(entity);
                    }
                    else
                        return false;
                }
                else
                    return false;
            }

            return true;
        }

        private async Task<bool> InvokeUpdateAsync(EntityEntry entry)
        {
            var entity = entry.Entity;
            var entityType = GetEntityType(entity);
            if (_dataQueries.ContainsKey(entityType))
            {
                EntityUpdate?.Invoke(this, new EntityEventArgs(entity));

                if (entry.State == EntityEntry.States.Modified)
                {
                    entry.OnCommit(entity);

                    if (entry.State == EntityEntry.States.Modified)
                    {
                        var ds = _dataQueries[entityType];
                        await (Task)ds.GetType().GetMethod("UpdateAsync").Invoke(ds, new[] { entity });

                        entry.OnCommitted(entity);
                    }
                    else
                        return false;
                }
                else
                    return false;
            }

            return true;
        }

        private bool InvokeDelete(EntityEntry entry)
        {
            var entity = entry.Entity;
            var entityType = GetEntityType(entity);
            if (_dataQueries.ContainsKey(entityType))
            {
                EntityDelete?.Invoke(this, new EntityEventArgs(entity));

                if (entry.State == EntityEntry.States.Deleted)
                {
                    entry.OnCommit(entity);

                    if (entry.State == EntityEntry.States.Deleted)
                    {
                        var ds = _dataQueries[entityType];
                        ds.GetType().GetMethod("Delete").Invoke(ds, new[] { entity });

                        entry.OnCommitted(entity);
                    }
                    else
                        return false;
                }
                else
                    return false;
            }

            return true;
        }

        private async Task<bool> InvokeDeleteAsync(EntityEntry entry)
        {
            var entity = entry.Entity;
            var entityType = GetEntityType(entity);
            if (_dataQueries.ContainsKey(entityType))
            {
                EntityDelete?.Invoke(this, new EntityEventArgs(entity));

                if (entry.State == EntityEntry.States.Deleted)
                {
                    entry.OnCommit(entity);

                    if (entry.State == EntityEntry.States.Deleted)
                    {
                        var ds = _dataQueries[entityType];
                        await (Task)ds.GetType().GetMethod("DeleteAsync").Invoke(ds, new[] { entity });

                        entry.OnCommitted(entity);
                    }
                    else
                        return false;
                }
                else
                    return false;
            }

            return true;
        }

        private Type GetEntityType(object entity)
        {
            var entityType = entity.GetType();
            return typeof(IObjectProxy).IsAssignableFrom(entityType) ? entityType.BaseType : entityType;
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Free other state (managed objects)
                }

                // Free your own state
                _entityEntries.Clear();
                _dataStores.Clear();

                //
                _disposed = true;
            }

            base.Dispose(disposing);
        }

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);

            base.Dispose();
        }

        ~UnitOfWorkBase()
        {
            Dispose(false);
        }
    }
}
