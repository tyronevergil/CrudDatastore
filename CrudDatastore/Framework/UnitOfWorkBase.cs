using CrudDatastore.Foundation;
using CrudDatastore.Framework.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using ObjectAction = System.Action<object>;

namespace CrudDatastore.Framework
{
    public abstract class UnitOfWorkBase : QueryUnitBase, IUnitOfWork
    {
        private bool _disposed;

        private readonly EntityChangeTracker _changeTracker;
        private readonly CommitProcessor _commitProcessor;
        private readonly RelatedEntityPropagationService _relatedEntityPropagation;
        private readonly ReflectionMethodCache _methodCache = new ReflectionMethodCache();
        private readonly object _dispatchSync = new object();
        private readonly IDictionary<Type, ObjectAction> _markModifiedDispatch = new Dictionary<Type, ObjectAction>();
        private readonly IDictionary<Type, Func<object, object, Delegate>> _markNewFactoryDispatch = new Dictionary<Type, Func<object, object, Delegate>>();
        private readonly IDictionary<Type, Func<object, Delegate>> _markDeletedFactoryDispatch = new Dictionary<Type, Func<object, Delegate>>();

        private readonly IDictionary<Type, IDataStore> _dataStores = new Dictionary<Type, IDataStore>();
        private readonly Dictionary<EntityBase, EntityEntry> _entityEntries = new Dictionary<EntityBase, EntityEntry>();

        public event EventHandler<EntityEventArgs> EntityCreate;
        public event EventHandler<EntityEventArgs> EntityUpdate;
        public event EventHandler<EntityEventArgs> EntityDelete;

        protected UnitOfWorkBase()
        {
            _changeTracker = new EntityChangeTracker(_entityEntries, _materializedEntities);
            _relatedEntityPropagation = new RelatedEntityPropagationService(_dataMapping, _dataQueries);
            _commitProcessor = new CommitProcessor(
                _dataQueries,
                GetEntityType,
                (args) => EntityCreate?.Invoke(this, args),
                (args) => EntityUpdate?.Invoke(this, args),
                (args) => EntityDelete?.Invoke(this, args),
                () =>
                {
                    _changeTracker.EntityEntries.Clear();
                    _materializedEntities.Clear();
                });
        }

        public virtual void MarkNew<T>(T entity) where T : EntityBase
        {
            _changeTracker.MarkEntityState(entity, EntityEntry.States.New, (e) => { }, (e) => { });
        }

        public virtual Task MarkNewAsync<T>(T entity) where T : EntityBase
        {
            MarkNew(entity);
            return Task.CompletedTask;
        }

        public virtual void MarkModified<T>(T entity) where T : EntityBase
        {
            _changeTracker.MarkEntityState(entity, EntityEntry.States.Modified, (e) => { }, (e) => { });
        }

        public virtual Task MarkModifiedAsync<T>(T entity) where T : EntityBase
        {
            MarkModified(entity);
            return Task.CompletedTask;
        }

        public virtual void MarkDeleted<T>(T entity) where T : EntityBase
        {
            _changeTracker.MarkEntityState(entity, EntityEntry.States.Deleted, (e) => { }, (e) => { });
        }

        public virtual Task MarkDeletedAsync<T>(T entity) where T : EntityBase
        {
            MarkDeleted(entity);
            return Task.CompletedTask;
        }

        public virtual void Commit()
        {
            _commitProcessor.Commit(_changeTracker.EntityEntries, DetectChanges);
        }

        public virtual async Task CommitAsync()
        {
            await _commitProcessor.CommitAsync(_changeTracker.EntityEntries, DetectChangesAsync).ConfigureAwait(false);
        }

        protected virtual IPropertyMap<T> Register<T>(IDataStore<T> dataStore) where T : EntityBase
        {
            return base.Register(dataStore);
        }

        protected override object CreateRelatedEntityCollection(object entity, object expressionPredicate, Type relatedEntityType, object relatedData)
        {
            var paramMarkNew = GetMarkNewFactory(relatedEntityType)(entity, expressionPredicate);
            var paramMarkDeleted = GetMarkDeletedFactory(relatedEntityType)(entity);

            var relatedEntityCollectionType = typeof(EntityCollection<>).MakeGenericType(new[] { relatedEntityType });
            var relatedEntityCollectionObject = Activator.CreateInstance(relatedEntityCollectionType, new[] { relatedData, paramMarkNew, paramMarkDeleted });

            return relatedEntityCollectionObject;
        }

        private async Task DetectChangesAsync()
        {
            _changeTracker.DetectMaterializedEntityChanges(MarkModifiedObject);
            await _relatedEntityPropagation.PropagateAsync(
                _changeTracker.EntityEntries.Values,
                GetEntityType,
                CreateMarkNewOnCommitUpdatePropertiesDelegate).ConfigureAwait(false);
        }

        private void DetectChanges()
        {
            DetectChangesAsync().GetAwaiter().GetResult();
        }

        private Action<T> MarkNewOnCommitUpdateProperties<T>(object parent, Expression expression) where T : EntityBase
        {
            var properties = ConstantPropertiesVisitor<T>.GetProperties(expression);

            return (e) =>
            {
                _changeTracker.MarkEntityState(e, EntityEntry.States.New,

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

                            var mappingEntity = MappingEntityHelper.CreateMappingEntity(mappingEntityType, parent, parentType, obj, entityType);

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

                        var mappingEntity = MappingEntityHelper.CreateMappingEntity(mappingEntityType, parent, parentType, obj, entityType);

                        var add = table.GetType().GetMethod("Add");
                        add.Invoke(table, new[] { mappingEntity });
                    }

                    foreach (var prop in entityType.GetProperties().Where(p => p.GetAccessors().Any(a => a.IsVirtual && !a.IsFinal)))
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
                _changeTracker.MarkEntityState(e, EntityEntry.States.Deleted,

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

                        var mappingEntity = MappingEntityHelper.CreateMappingEntity(mappingEntityType, parent, parentType, obj, entityType);

                        var delete = table.GetType().GetMethod("Delete");
                        delete.Invoke(table, new[] { mappingEntity });

                        var param = Expression.Parameter(mappingEntityType, "e");
                        var expression = MappingEntityHelper.BuildEntityMatchExpression(mappingEntityType, entityType, obj, param);

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

        private void MarkModifiedObject(object entity)
        {
            GetMarkModifiedDispatcher(entity.GetType())(entity);
        }

        private Delegate CreateMarkNewOnCommitUpdatePropertiesDelegate(object parent, object expressionPredicate, Type relatedEntityType)
        {
            return GetMarkNewFactory(relatedEntityType)(parent, expressionPredicate);
        }

        private ObjectAction GetMarkModifiedDispatcher(Type entityType)
        {
            lock (_dispatchSync)
            {
                ObjectAction dispatcher;
                if (!_markModifiedDispatch.TryGetValue(entityType, out dispatcher))
                {
                    var method = _methodCache.GetClosedGenericMethod(typeof(UnitOfWorkBase), nameof(MarkModified), entityType, BindingFlags.Instance | BindingFlags.Public);
                    dispatcher = (entity) => method.Invoke(this, new[] { entity });
                    _markModifiedDispatch[entityType] = dispatcher;
                }

                return dispatcher;
            }
        }

        private Func<object, object, Delegate> GetMarkNewFactory(Type relatedEntityType)
        {
            lock (_dispatchSync)
            {
                Func<object, object, Delegate> factory;
                if (!_markNewFactoryDispatch.TryGetValue(relatedEntityType, out factory))
                {
                    var method = _methodCache.GetClosedGenericMethod(typeof(UnitOfWorkBase), nameof(MarkNewOnCommitUpdateProperties), relatedEntityType, BindingFlags.NonPublic | BindingFlags.Instance);
                    factory = (parent, predicate) => (Delegate)method.Invoke(this, new[] { parent, predicate });
                    _markNewFactoryDispatch[relatedEntityType] = factory;
                }

                return factory;
            }
        }

        private Func<object, Delegate> GetMarkDeletedFactory(Type relatedEntityType)
        {
            lock (_dispatchSync)
            {
                Func<object, Delegate> factory;
                if (!_markDeletedFactoryDispatch.TryGetValue(relatedEntityType, out factory))
                {
                    var method = _methodCache.GetClosedGenericMethod(typeof(UnitOfWorkBase), nameof(MarkDeletedOnCommitForDeletion), relatedEntityType, BindingFlags.NonPublic | BindingFlags.Instance);
                    factory = (parent) => (Delegate)method.Invoke(this, new[] { parent });
                    _markDeletedFactoryDispatch[relatedEntityType] = factory;
                }

                return factory;
            }
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

                _entityEntries.Clear();
                _dataStores.Clear();

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
