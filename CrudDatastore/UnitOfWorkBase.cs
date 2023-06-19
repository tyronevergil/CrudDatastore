using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace CrudDatastore
{
    public abstract class UnitOfWorkBase : IUnitOfWork, IDataNavigation
    {
        private bool _disposed;

        private readonly IDictionary<Type, IDataStore> _dataStores = new Dictionary<Type, IDataStore>();
        private readonly IDictionary<PropertyInfo, Delegate> _dataMapping = new Dictionary<PropertyInfo, Delegate>();
        private readonly IDictionary<Tuple<Type, Type>, Type> _dataTableMapping = new Dictionary<Tuple<Type, Type>, Type>();

        private readonly Dictionary<EntityBase, EntityBase> _materializedEntities = new Dictionary<EntityBase, EntityBase>();
        private readonly Dictionary<EntityBase, EntityEntry> _entityEntries = new Dictionary<EntityBase, EntityEntry>();

        public event EventHandler<EntityEventArgs> EntityMaterialized;
        public event EventHandler<EntityEventArgs> EntityCreate;
        public event EventHandler<EntityEventArgs> EntityUpdate;
        public event EventHandler<EntityEventArgs> EntityDelete;

        protected virtual IPropertyMap<T> Register<T>(IDataStore<T> dataStore) where T : EntityBase
        {
            var type = typeof(T);
            if (!_dataStores.ContainsKey(type))
            {
                _dataStores.Add(type, new DataStore<T>((ICrud<T>)dataStore, MaterializeEntityObject));
            }

            return new PropertyMap<T>(_dataMapping, _dataTableMapping);
        }

        public virtual void Execute(string command, params object[] parameters)
        {

        }

        public virtual IDataQuery<T> Read<T>() where T : EntityBase
        {
            var type = typeof(T);
            if (_dataStores.ContainsKey(type))
                return (IDataQuery<T>)_dataStores[type];

            return null;
        }

        public virtual void MarkNew<T>(T entity) where T : EntityBase
        {
            MarkNew(entity, (e) => { }, (e) => { });
        }

        private void MarkNew<T>(T entity, Action<object> onCommit, Action<object> onCommitted) where T : EntityBase
        {
            if (_entityEntries.ContainsKey(entity))
            {
                _entityEntries[entity].ChangeState(EntityEntry.States.New);
            }
            else
            {
                _entityEntries.Add(entity, new EntityEntry(EntityEntry.States.New, entity, entity, onCommit, onCommitted));
            }
        }

        public virtual void MarkModified<T>(T entity) where T : EntityBase
        {
            if (_entityEntries.ContainsKey(entity))
            {
                _entityEntries[entity].ChangeState(EntityEntry.States.Modified);
            }
            else
            {
                var entry = _materializedEntities.ContainsValue(entity) ? _materializedEntities.FirstOrDefault(m => m.Value == entity).Key : null;
                _entityEntries.Add(entity, new EntityEntry(EntityEntry.States.Modified, entry, entity, (e) => { }, (e) => { }));
            }
        }

        public virtual void MarkDeleted<T>(T entity) where T : EntityBase
        {
            MarkDeleted(entity, (e) => { }, (e) => { });
        }

        private void MarkDeleted<T>(T entity, Action<object> onCommit, Action<object> onCommitted) where T : EntityBase
        {
            if (_entityEntries.ContainsKey(entity))
            {
                _entityEntries[entity].ChangeState(EntityEntry.States.Deleted);
            }
            else
            {
                var entry = _materializedEntities.ContainsValue(entity) ? _materializedEntities.FirstOrDefault(m => m.Value == entity).Key : null;
                _entityEntries.Add(entity, new EntityEntry(EntityEntry.States.Deleted, entry, entity, onCommit, onCommitted));
            }
        }

        public virtual void Commit()
        {
            DetectChanges();

            while (true)
            {
                var entry = _entityEntries.Select(e => e.Value).Where(e => e.UnCommited).OrderBy(e => e.State).FirstOrDefault();
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

        private Type GetEntityType(object entity)
        {
            var entityType = entity.GetType();
            return typeof(IEntityProxy).IsAssignableFrom(entityType) ? entityType.BaseType : entityType;
        }

        private bool InvokeCreate(EntityEntry entry)
        {
            var entity = entry.Entity;
            var entityType = GetEntityType(entity);
            if (_dataStores.ContainsKey(entityType))
            {
                EntityCreate?.Invoke(this, new EntityEventArgs(entity));

                if (entry.State == EntityEntry.States.New)
                {
                    entry.OnCommit(entity);

                    if (entry.State == EntityEntry.States.New)
                    {
                        var ds = _dataStores[entityType];
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

        private bool InvokeUpdate(EntityEntry entry)
        {
            var entity = entry.Entity;
            var entityType = GetEntityType(entity);
            if (_dataStores.ContainsKey(entityType))
            {
                EntityUpdate?.Invoke(this, new EntityEventArgs(entity));

                if (entry.State == EntityEntry.States.Modified)
                {
                    entry.OnCommit(entity);

                    if (entry.State == EntityEntry.States.Modified)
                    {
                        var ds = _dataStores[entityType];
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

        private bool InvokeDelete(EntityEntry entry)
        {
            var entity = entry.Entity;
            var entityType = GetEntityType(entity);
            if (_dataStores.ContainsKey(entityType))
            {
                EntityDelete?.Invoke(this, new EntityEventArgs(entity));

                if (entry.State == EntityEntry.States.Deleted)
                {
                    entry.OnCommit(entity);

                    if (entry.State == EntityEntry.States.Deleted)
                    {
                        var ds = _dataStores[entityType];
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

        private Action<T> MarkNewOnCommitUpdateProperties<T>(object parent, Expression expression) where T : EntityBase
        {
            var properties = ConstantPropertiesVisitor<T>.GetProperties(expression);

            return (e) =>
            {
                MarkNew(e,

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
                        if ((typeof(IEntityProxy).IsAssignableFrom(obj.GetType())))
                        {
                            var mappingEntityType = _dataTableMapping[keyTypes];
                            var table = _dataStores[mappingEntityType];

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
                        var table = _dataStores[mappingEntityType];

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

                });
            };
        }

        private Action<T> MarkDeletedOnCommitForDeletion<T>(object parent) where T : EntityBase
        {
            return (e) =>
            {
                MarkDeleted(e,

                /* on commit */
                (obj) =>
                {
                    var parentType = GetEntityType(parent);
                    var entityType = typeof(T);

                    var keyTypes = Tuple.Create(parentType, entityType);
                    if (_dataTableMapping.ContainsKey(keyTypes))
                    {
                        var mappingEntityType = _dataTableMapping[keyTypes];
                        var table = _dataStores[mappingEntityType];

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

        private object GetRelatedPropertyValue(object entry, PropertyInfo prop)
        {
            return GetRelatedPropertyValue(entry, prop, true, false);
        }

        private object GetRelatedPropertyValue(object entry, PropertyInfo prop, bool isNullOnEmptyResult)
        {
            return GetRelatedPropertyValue(entry, prop, true, isNullOnEmptyResult);
        }

        private object GetRelatedPropertyValue(object entry, PropertyInfo prop, bool isMarkOnCommit, bool isNullOnEmptyResult)
        {
            object value = null;

            if (_dataMapping.ContainsKey(prop))
            {
                var expressionPredicate = _dataMapping[prop].DynamicInvoke(entry, _dataStores);

                var propType = prop.PropertyType;
                var isGenericType = propType.IsGenericType;

                var relatedEntityType = isGenericType ? propType.GetGenericArguments().First() : propType;
                if (_dataStores.ContainsKey(relatedEntityType))
                {
                    var ds = _dataStores[relatedEntityType];

                    var specType = typeof(Specification<>).MakeGenericType(new[] { relatedEntityType });
                    var specObject = Activator.CreateInstance(specType, new[] { expressionPredicate });

                    var relatedData = ds.GetType().GetMethod("Find").Invoke(ds, new[] { specObject });

                    if (isGenericType)
                    {
                        object paramMarkNew = null;
                        object paramMarkDeleted = null;

                        if (isMarkOnCommit)
                        {
                            var unitOfWorkType = typeof(UnitOfWorkBase);

                            paramMarkNew = unitOfWorkType
                                .GetMethod("MarkNewOnCommitUpdateProperties", BindingFlags.NonPublic | BindingFlags.Instance).MakeGenericMethod(new[] { relatedEntityType })
                                .Invoke(this, new[] { entry, expressionPredicate });

                            paramMarkDeleted = unitOfWorkType
                                .GetMethod("MarkDeletedOnCommitForDeletion", BindingFlags.NonPublic | BindingFlags.Instance).MakeGenericMethod(new[] { relatedEntityType })
                                .Invoke(this, new object[] { entry });

                            //var paramType = typeof(Action<>).MakeGenericType(new[] { relatedEntityType });
                            //var paramMarkNew = Delegate.CreateDelegate(paramType, this, unitOfWorkType.GetMethod("MarkNew").MakeGenericMethod(new[] { relatedEntityType }));
                            //var paramMarkDeleted = Delegate.CreateDelegate(paramType, this, unitOfWorkType.GetMethod("MarkDeleted").MakeGenericMethod(new[] { relatedEntityType }));
                        }

                        var relatedEntityCollectionType = typeof(EntityCollection<>).MakeGenericType(new[] { relatedEntityType });
                        var relatedEntityCollectionObject = Activator.CreateInstance(relatedEntityCollectionType, new[] { relatedData, paramMarkNew, paramMarkDeleted });

                        value = relatedEntityCollectionObject;

                        if (isNullOnEmptyResult)
                        {
                            var any = typeof(Enumerable)
                                    .GetGenericMethod("Any", new[] { typeof(IEnumerable<>) })
                                    .MakeGenericMethod(new Type[] { relatedEntityType });
                            var isAny = (bool)any.Invoke(null, new[] { relatedEntityCollectionObject });
                            if (!isAny)
                                value = null;
                        }
                    }
                    else
                    {
                        var first = typeof(Queryable)
                                .GetGenericMethod("FirstOrDefault", new[] { typeof(IQueryable<>) })
                                .MakeGenericMethod(new Type[] { relatedEntityType });
                        var firstEntityObject = first.Invoke(null, new[] { relatedData });

                        value = firstEntityObject;
                    }
                }
            }

            return value;
        }

        private T MaterializeEntityObject<T>(T entry) where T : EntityBase
        {
            if (entry == null)
            {
                return null;
            }

            if (_materializedEntities.ContainsKey(entry))
            {
                return (T)_materializedEntities[entry];
            }

            var entityType = typeof(T);
            var obj = Activator.CreateInstance(ProxyBuilder.CreateProxyType(entityType), new[] { new Interceptor(new Dictionary<string, object>(),
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
                            var prop = entry.GetType().GetProperty(propName);
                            var value = prop.GetValue(entry, null);
                            if (value == null)
                            {
                                if (propType.IsValueType)
                                {
                                    value = Activator.CreateInstance(propType);
                                }
                                else
                                {
                                    value = GetRelatedPropertyValue(entry, prop);
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
                })});

            foreach (var prop in entityType.GetProperties().Where(p => p.GetAccessors().Any(a => !(a.IsVirtual && !a.IsFinal))))
            {
                object value = null;

                value = prop.GetValue(entry, null);
                if (value == null)
                {
                    value = GetRelatedPropertyValue(entry, prop, true);
                }

                if (value != null)
                    prop.SetValue(obj, value, null);
            }

            if (obj != null)
            {
                _materializedEntities.Add(entry, (T)obj);
            }

            EntityMaterialized?.Invoke(this, new EntityEventArgs(obj));

            return (T)obj;
        }

        private void DetectChanges()
        {
            var entryList = _entityEntries.Select(e => e.Value).Where(e => e.State == EntityEntry.States.New || e.State == EntityEntry.States.Modified).ToList();
            var i = 0;
            while (i < entryList.Count)
            {
                var e = entryList[i];
                var entry = e.Entry;
                var entity = e.Entity;

                var entityType = GetEntityType(entity);
                foreach (var prop in entityType.GetProperties().Where(p => typeof(EntityBase).IsAssignableFrom(p.PropertyType) || (p.PropertyType.IsGenericType && typeof(EntityBase).IsAssignableFrom(p.PropertyType.GetGenericArguments().First()))))
                {
                    var propValue = prop.GetValue(entity);
                    var propType = propValue != null ? propValue.GetType() : prop.PropertyType;
                    if (!(typeof(IEntityProxy).IsAssignableFrom(propType) || typeof(IEntityCollection).IsAssignableFrom(propType)))
                    {
                        if (_dataMapping.ContainsKey(prop))
                        {
                            var expressionPredicate = _dataMapping[prop].DynamicInvoke(entry, _dataStores);

                            var relatedEntityType = prop.PropertyType.IsGenericType ? prop.PropertyType.GetGenericArguments().First() : prop.PropertyType;
                            if (_dataStores.ContainsKey(relatedEntityType))
                            {
                                var unitOfWorkType = typeof(UnitOfWorkBase);
                                var markNew = (Delegate)unitOfWorkType
                                    .GetMethod("MarkNewOnCommitUpdateProperties", BindingFlags.NonPublic | BindingFlags.Instance).MakeGenericMethod(new[] { relatedEntityType })
                                    .Invoke(this, new[] { entry, expressionPredicate });

                                if (prop.PropertyType.IsGenericType)
                                {
                                    var entityPropCollection = propValue as IEnumerable;
                                    var entryPropCollection = (prop.GetValue(entry) ?? GetRelatedPropertyValue(entry, prop)) as IEnumerable;

                                    if (entryPropCollection != null && (entityPropCollection == null || (entityPropCollection != null && entityPropCollection != entryPropCollection)))
                                    {
                                        var markDeleted = typeof(UnitOfWorkBase).GetMethod("MarkDeleted");
                                        foreach (var item in entryPropCollection)
                                        {
                                            markDeleted.MakeGenericMethod(new[] { relatedEntityType }).Invoke(this, new[] { item });
                                        }
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
                                    markNew.DynamicInvoke(prop.GetValue(entity));
                                }

                            }
                        }
                    }
                }

                i++;
            }

            var markModified = typeof(UnitOfWorkBase).GetMethod("MarkModified");
            foreach (var item in _materializedEntities)
            {
                var entry = item.Key;
                var entity = item.Value;
                if (entry.GetType().GetProperties().Where(prop => prop.PropertyType.IsValueType || Type.GetTypeCode(prop.PropertyType) == TypeCode.String).Any(prop => !Equals(prop.GetValue(entry, null), prop.GetValue(entity, null))))
                {
                    markModified.MakeGenericMethod(new[] { entry.GetType() }).Invoke(this, new[] { entity });
                }
            }
        }

        IPropertyMap<T> IDataMapping.Register<T>(IDataStore<T> dataStore)
        {
            return Register<T>(dataStore);
        }

        IPropertyMap<T1> IDataMapping.Map<T1, T2>(Expression<Func<T1, T2>> prop, Expression<Func<T1, T2, bool>> predicate)
        {
            var propMap = new PropertyMap<T1>(_dataMapping, _dataTableMapping);
            return propMap.Map(prop, predicate);
        }

        IPropertyMap<T1> IDataMapping.Map<T1, T2>(Expression<Func<T1, ICollection<T2>>> prop, Expression<Func<T1, T2, bool>> predicate)
        {
            var propMap = new PropertyMap<T1>(_dataMapping, _dataTableMapping);
            return propMap.Map(prop, predicate);
        }

        IPropertyMap<T1> IDataMapping.Map<T1, T2, T3>(Expression<Func<T1, ICollection<T2>>> prop, Expression<Func<T3, Expression<Func<T1, T2, bool>>>> mapping)
        {
            var propMap = new PropertyMap<T1>(_dataMapping, _dataTableMapping);
            return propMap.Map(prop, mapping);
        }

        bool IDataMapping.IsRegistered<T>()
        {
            return _dataStores.ContainsKey(typeof(T));
        }

        Type ResolveEntityType(object entry)
        {
            var entityType = entry.GetType();

            //temporary resolution
            if (entityType.FullName.StartsWith("System.Data.Entity.DynamicProxies", StringComparison.Ordinal))
                entityType = entityType.BaseType;

            return entityType;
        }

        object IDataNavigation.GetNavigationProperty(object entry, string prop)
        {
            var entityType = ResolveEntityType(entry);
            return GetRelatedPropertyValue(entry, entityType.GetProperty(prop), false, false);
        }

        object IDataNavigation.ResolveNavigationProperties(object entry)
        {
            // should merge with materializeentityobject
            var entityType = ResolveEntityType(entry);
            foreach (var prop in entityType.GetProperties().Where(p => typeof(EntityBase).IsAssignableFrom(p.PropertyType) || (p.PropertyType.IsGenericType && typeof(EntityBase).IsAssignableFrom(p.PropertyType.GetGenericArguments().First()))))
            {
                object value = null;

                value = prop.GetValue(entry, null);
                if (value == null)
                {
                    value = GetRelatedPropertyValue(entry, prop, false);

                    if (value != null)
                        prop.SetValue(entry, value, null);
                }
            }

            return entry;
        }

        void IDataNavigation.ResolveAddedNavigationProperties(object entry)
        {
            // should merge with detectchanges
            var entityType = ResolveEntityType(entry);
            foreach (var prop in entityType.GetProperties().Where(p => typeof(EntityBase).IsAssignableFrom(p.PropertyType) || (p.PropertyType.IsGenericType && typeof(EntityBase).IsAssignableFrom(p.PropertyType.GetGenericArguments().First()))))
            {
                if (!(typeof(IEntityProxy).IsAssignableFrom(prop.PropertyType) || typeof(IEntityCollection).IsAssignableFrom(prop.PropertyType)))
                {
                    if (_dataMapping.ContainsKey(prop))
                    {
                        var expressionPredicate = _dataMapping[prop].DynamicInvoke(entry, _dataStores);

                        var relatedEntityType = prop.PropertyType.IsGenericType ? prop.PropertyType.GetGenericArguments().First() : prop.PropertyType;
                        if (_dataStores.ContainsKey(relatedEntityType))
                        {
                            var unitOfWorkType = typeof(UnitOfWorkBase);
                            var markNew = (Delegate)unitOfWorkType
                                .GetMethod("MarkNewOnCommitUpdateProperties", BindingFlags.NonPublic | BindingFlags.Instance).MakeGenericMethod(new[] { relatedEntityType })
                                .Invoke(this, new[] { entry, expressionPredicate });

                            if (prop.PropertyType.IsGenericType)
                            {
                                var collection = prop.GetValue(entry) as IEnumerable;
                                if (collection != null)
                                {
                                    foreach (var item in collection)
                                    {
                                        markNew.DynamicInvoke(item);
                                    }
                                }
                            }
                            else
                            {
                                markNew.DynamicInvoke(prop.GetValue(entry));
                            }
                        }
                    }
                }
            }
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
                _entityEntries.Clear();
                _materializedEntities.Clear();
                _dataMapping.Clear();
                _dataStores.Clear();

                //
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~UnitOfWorkBase()
        {
            Dispose(false);
        }
    }

    public class EntityEventArgs : EventArgs
    {
        private readonly object _entity;

        public EntityEventArgs(object entity)
        {
            _entity = entity;
        }

        public object Entity
        {
            get
            {
                return _entity;
            }
        }
    }

    internal class EntityEntry
    {
        public States State { get; private set; }
        public object Entry { get; private set; }
        public object Entity { get; private set; }
        public Action<object> OnCommit { get; private set; }
        public Action<object> OnCommitted { get; private set; }

        public EntityEntry(States state, object entry, object entity, Action<object> onCommit, Action<object> onCommitted)
        {
            State = state;
            Entry = entry;
            Entity = entity;
            OnCommit = onCommit;
            OnCommitted = onCommitted;
        }

        public void ChangeState(States state)
        {
            if (State != state)
            {
                State = state;
                OnCommit = (e) => { };
                OnCommitted = (e) => { };
            }
        }

        public void Commit()
        {
            State = States.Commited;
        }

        public bool UnCommited
        {
            get { return State != States.Commited; }
        }

        internal enum States
        {
            New = 1,
            Modified,
            Deleted,
            Commited
        }
    }

    public interface IPropertyMap<T1> where T1 : EntityBase
    {
        IPropertyMap<T1> Map<T2>(Expression<Func<T1, T2>> prop, Expression<Func<T1, T2, bool>> predicate) where T2 : EntityBase;
        IPropertyMap<T1> Map<T2>(Expression<Func<T1, ICollection<T2>>> prop, Expression<Func<T1, T2, bool>> predicate) where T2 : EntityBase;
        IPropertyMap<T1> Map<T2, T3>(Expression<Func<T1, ICollection<T2>>> prop, Expression<Func<T3, Expression<Func<T1, T2, bool>>>> mapping) where T2 : EntityBase where T3 : EntityBase;
    }

    internal class PropertyMap<T1> : IPropertyMap<T1> where T1 : EntityBase
    {
        private readonly IDictionary<PropertyInfo, Delegate> _dataMapping;
        private readonly IDictionary<Tuple<Type, Type>, Type> _dataTableMapping;

        public PropertyMap(IDictionary<PropertyInfo, Delegate> dataMapping, IDictionary<Tuple<Type, Type>, Type> dataTableMapping)
        {
            _dataMapping = dataMapping;
            _dataTableMapping = dataTableMapping;
        }

        public IPropertyMap<T1> Map<T2>(Expression<Func<T1, T2>> prop, Expression<Func<T1, T2, bool>> predicate) where T2 : EntityBase
        {
            PropertyInfo propInfo = (PropertyInfo)((MemberExpression)prop.Body).Member;

            Func<T1, IDictionary<Type, IDataStore>, Expression<Func<T2, bool>>> predicateBuilder = (t1, dataStores) =>
            {
                var param = predicate.Parameters.FirstOrDefault(p => p.Type == typeof(T2));
                var body = (new ParameterReplacerVisitor(Expression.Constant(t1))).Visit(predicate.Body);

                return Expression.Lambda<Func<T2, bool>>(body, new[] { param });
            };

            return Map(propInfo, predicateBuilder);
        }

        public IPropertyMap<T1> Map<T2>(Expression<Func<T1, ICollection<T2>>> prop, Expression<Func<T1, T2, bool>> predicate) where T2 : EntityBase
        {
            PropertyInfo propInfo = (PropertyInfo)((MemberExpression)prop.Body).Member;

            Func<T1, IDictionary<Type, IDataStore>, Expression<Func<T2, bool>>> predicateBuilder = (t1, dataStores) =>
            {
                var param = predicate.Parameters.FirstOrDefault(p => p.Type == typeof(T2));
                var body = (new ParameterReplacerVisitor(Expression.Constant(t1))).Visit(predicate.Body);

                return Expression.Lambda<Func<T2, bool>>(body, new[] { param });
            };

            return Map(propInfo, predicateBuilder);
        }

        public IPropertyMap<T1> Map<T2, T3>(Expression<Func<T1, ICollection<T2>>> prop, Expression<Func<T3, Expression<Func<T1, T2, bool>>>> mapping) where T2 : EntityBase where T3 : EntityBase
        {
            PropertyInfo propInfo = (PropertyInfo)((MemberExpression)prop.Body).Member;

            Func<T1, IDictionary<Type, IDataStore>, Expression<Func<T2, bool>>> predicateBuilder = (t1, dataStores) =>
            {
                var table = (IDataStore<T3>)dataStores[typeof(T3)];

                var paramT3 = mapping.Parameters.FirstOrDefault(p => p.Type == typeof(T3));
                var bodyT3 = ((LambdaExpression)((UnaryExpression)mapping.Body).Operand).Body;

                var expressionT3 = Expression.Call(typeof(Queryable), "Any", new[] { typeof(T3) }, Expression.Constant(table.Find(new Specification<T3>(m => true))),
                        Expression.Lambda<Func<T3, bool>>(bodyT3, new[] { paramT3 }));

                var expressionT1 = (new ParameterReplacerVisitor(Expression.Constant(t1))).Visit(expressionT3);

                var paramT2 = Expression.Parameter(typeof(T2));
                var expressionT2 = (new ParameterReplacerVisitor(paramT2)).Visit(expressionT1);


                var expression = Expression.Lambda<Func<T2, bool>>(expressionT2, new[] { paramT2 });
                return expression;
            };

            var keyTypes = Tuple.Create(typeof(T1), typeof(T2));
            if (!_dataTableMapping.ContainsKey(keyTypes))
            {
                _dataTableMapping.Add(keyTypes, typeof(T3));
            }

            return Map(propInfo, predicateBuilder);
        }

        private IPropertyMap<T1> Map<T2>(PropertyInfo prop, Func<T1, IDictionary<Type, IDataStore>, Expression<Func<T2, bool>>> predicateBuilder)
        {
            if (!_dataMapping.ContainsKey(prop))
            {
                _dataMapping.Add(prop, predicateBuilder);
            }

            return new PropertyMap<T1>(_dataMapping, _dataTableMapping);
        }

        private class ParameterReplacerVisitor : ExpressionVisitor
        {
            private readonly Expression _expression;

            public ParameterReplacerVisitor(Expression expression)
            {
                _expression = expression;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                if (node.Type.IsAssignableFrom(_expression.Type))
                    return _expression;

                return base.VisitParameter(node);
            }
        }
    }

    internal class ConstantPropertiesVisitor<T> : ExpressionVisitor where T : EntityBase
    {
        private readonly Stack _pathToValue = new Stack();
        private readonly IDictionary<string, IDictionary<string, object>> _properties = new Dictionary<string, IDictionary<string, object>>();

        public static IDictionary<string, IDictionary<string, object>> GetProperties(Expression expression)
        {
            var visitor = new ConstantPropertiesVisitor<T>();
            visitor.Visit(expression);
            return visitor.GetProperties();
        }

        private ConstantPropertiesVisitor()
        {
        }

        private IDictionary<string, IDictionary<string, object>> GetProperties()
        {
            return _properties;
        }

        public override Expression Visit(Expression node)
        {
            _pathToValue.Push(node);
            var result = base.Visit(node);
            _pathToValue.Pop();

            return result;
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            var pathArray = _pathToValue.ToArray();
            var parentMemberExpression = pathArray.FirstOrDefault(e =>
            {
                var expression = ((Expression)e);
                if (expression.NodeType == ExpressionType.MemberAccess)
                {
                    var memberExpression = ((MemberExpression)expression);
                    if (memberExpression.Member is PropertyInfo && memberExpression.Expression is ConstantExpression)
                    {
                        return memberExpression.Expression.Type != typeof(T);
                    }
                }

                return false;
            });

            if (parentMemberExpression != null)
            {
                var memberExpression = (MemberExpression)parentMemberExpression;
                var propertyInfo = (PropertyInfo)memberExpression.Member;

                var prop = memberExpression.Member.Name;
                var value = propertyInfo.GetValue(node.Value, null); ;

                var comparisonExpression = pathArray.FirstOrDefault(e =>
                {
                    var expression = ((Expression)e);
                    if (expression is BinaryExpression binary)
                    {
                        var memberLeft = binary.Left as MemberExpression;
                        if (memberLeft != null && memberLeft.Member is PropertyInfo)
                        {
                            if (memberLeft == parentMemberExpression)
                            {
                                return true;
                            }
                        }

                        var memberRight = binary.Right as MemberExpression;
                        if (memberRight != null && memberRight.Member is PropertyInfo)
                        {
                            if (memberRight == parentMemberExpression)
                            {
                                return true;
                            }
                        }
                    }

                    return false;
                });

                if (comparisonExpression != null)
                {
                    var binaryExpression = (BinaryExpression)comparisonExpression;
                    var comparisonMemberExpression = (binaryExpression.Left == parentMemberExpression ? binaryExpression.Right : binaryExpression.Left) as MemberExpression;

                    if (comparisonMemberExpression != null)
                    {
                        var propValues = new Dictionary<string, object>();
                        propValues.Add("value", value);
                        propValues.Add("comparison", comparisonMemberExpression.Member.Name);

                        _properties.Add(prop, propValues);
                    }
                }
            }

            return base.VisitConstant(node);
        }
    }

    internal interface IEntityCollection
    {
    }

    internal class EntityCollection<T> : ICollection<T>, IQueryable<T>, IQuery<T>, IEntityCollection where T : EntityBase
    {
        private readonly IQueryable<T> _data;
        private readonly Lazy<IList<T>> _lazyList;

        private readonly Action<T> _markNew;
        private readonly Action<T> _markDeleted;

        public EntityCollection(IQueryable<T> data, Action<T> markNew, Action<T> markDeleted)
        {
            _data = data;
            _markNew = markNew;
            _markDeleted = markDeleted;

            _lazyList = new Lazy<IList<T>>(() =>
            {
                return _data.ToList();
            });
        }

        public int Count
        {
            //get { return _lazyList.IsValueCreated ? _lazyList.Value.Count : _data.Count(); }
            get { return _lazyList.Value.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public void Add(T item)
        {
            _lazyList.Value.Add(item);
            if (_markNew != null)
                _markNew(item);
        }

        public bool Remove(T item)
        {
            var b = _lazyList.Value.Remove(item);
            if (b)
            {
                if (_markDeleted != null)
                    _markDeleted(item);
            }

            return b;
        }

        public void Clear()
        {
            foreach (var item in _lazyList.Value)
            {
                Remove(item);
            }
        }

        public bool Contains(T item)
        {
            return _lazyList.Value.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _lazyList.Value.CopyTo(array, arrayIndex);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _lazyList.Value.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IQueryable<T> Execute(Expression<Func<T, bool>> predicate)
        {
            return _lazyList.Value.Where(predicate.Compile()).AsQueryable();
        }

        public IQueryable<T> Execute(string command, params object[] parameters)
        {
            return Execute(e => false);
        }

        public Type ElementType
        {
            get { return _data.ElementType; }
        }

        public Expression Expression
        {
            get { return _data.Expression; }
        }

        public IQueryProvider Provider
        {
            get { return _data.Provider; }
        }
    }

}
