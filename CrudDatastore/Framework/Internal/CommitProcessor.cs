using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using SysAction = System.Action;
using SysActionEntityEvent = System.Action<CrudDatastore.EntityEventArgs>;

namespace CrudDatastore.Framework.Internal
{
    internal sealed class CommitProcessor
    {
        private readonly IDictionary<Type, IDataQuery> _dataQueries;
        private readonly Func<object, Type> _getEntityType;
        private readonly SysActionEntityEvent _entityCreate;
        private readonly SysActionEntityEvent _entityUpdate;
        private readonly SysActionEntityEvent _entityDelete;
        private readonly SysAction _onCommitted;
        private readonly ReflectionMethodCache _methodCache = new ReflectionMethodCache();

        public CommitProcessor(
            IDictionary<Type, IDataQuery> dataQueries,
            Func<object, Type> getEntityType,
            SysActionEntityEvent entityCreate,
            SysActionEntityEvent entityUpdate,
            SysActionEntityEvent entityDelete,
            SysAction onCommitted)
        {
            _dataQueries = dataQueries;
            _getEntityType = getEntityType;
            _entityCreate = entityCreate;
            _entityUpdate = entityUpdate;
            _entityDelete = entityDelete;
            _onCommitted = onCommitted;
        }

        public void Commit(IDictionary<EntityBase, EntityEntry> entityEntries, SysAction detectChanges)
        {
            detectChanges?.Invoke();
            Execute(entityEntries, isAsync: false).GetAwaiter().GetResult();
            _onCommitted?.Invoke();
        }

        public async Task CommitAsync(IDictionary<EntityBase, EntityEntry> entityEntries, Func<Task> detectChangesAsync)
        {
            if (detectChangesAsync != null)
                await detectChangesAsync().ConfigureAwait(false);

            await Execute(entityEntries, isAsync: true).ConfigureAwait(false);
            _onCommitted?.Invoke();
        }

        private async Task Execute(IDictionary<EntityBase, EntityEntry> entityEntries, bool isAsync)
        {
            while (true)
            {
                var entry = entityEntries.Where(e => e.Value.UnCommited).Select(e => e.Value).OrderBy(e => e.State).FirstOrDefault();
                if (entry == null)
                    break;

                var shouldCommit = true;
                switch (entry.State)
                {
                    case EntityEntry.States.New:
                        shouldCommit = isAsync ? await InvokeCreateAsync(entry).ConfigureAwait(false) : InvokeCreate(entry);
                        break;
                    case EntityEntry.States.Modified:
                        shouldCommit = isAsync ? await InvokeUpdateAsync(entry).ConfigureAwait(false) : InvokeUpdate(entry);
                        break;
                    case EntityEntry.States.Deleted:
                        shouldCommit = isAsync ? await InvokeDeleteAsync(entry).ConfigureAwait(false) : InvokeDelete(entry);
                        break;
                }

                if (shouldCommit)
                    entry.Commit();
            }
        }

        private bool InvokeCreate(EntityEntry entry)
        {
            var entity = entry.Entity;
            var entityType = _getEntityType(entity);
            if (_dataQueries.ContainsKey(entityType))
            {
                _entityCreate?.Invoke(new EntityEventArgs(entity));

                if (entry.State == EntityEntry.States.New)
                {
                    entry.OnCommit(entity);

                    if (entry.State == EntityEntry.States.New)
                    {
                        var ds = _dataQueries[entityType];
                        GetCommandMethod(ds.GetType(), "Add").Invoke(ds, new[] { entity });

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
            var entityType = _getEntityType(entity);
            if (_dataQueries.ContainsKey(entityType))
            {
                _entityCreate?.Invoke(new EntityEventArgs(entity));

                if (entry.State == EntityEntry.States.New)
                {
                    entry.OnCommit(entity);

                    if (entry.State == EntityEntry.States.New)
                    {
                        var ds = _dataQueries[entityType];
                        await ((Task)GetCommandMethod(ds.GetType(), "AddAsync").Invoke(ds, new[] { entity })).ConfigureAwait(false);

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
            var entityType = _getEntityType(entity);
            if (_dataQueries.ContainsKey(entityType))
            {
                _entityUpdate?.Invoke(new EntityEventArgs(entity));

                if (entry.State == EntityEntry.States.Modified)
                {
                    entry.OnCommit(entity);

                    if (entry.State == EntityEntry.States.Modified)
                    {
                        var ds = _dataQueries[entityType];
                        GetCommandMethod(ds.GetType(), "Update").Invoke(ds, new[] { entity });

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
            var entityType = _getEntityType(entity);
            if (_dataQueries.ContainsKey(entityType))
            {
                _entityUpdate?.Invoke(new EntityEventArgs(entity));

                if (entry.State == EntityEntry.States.Modified)
                {
                    entry.OnCommit(entity);

                    if (entry.State == EntityEntry.States.Modified)
                    {
                        var ds = _dataQueries[entityType];
                        await ((Task)GetCommandMethod(ds.GetType(), "UpdateAsync").Invoke(ds, new[] { entity })).ConfigureAwait(false);

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
            var entityType = _getEntityType(entity);
            if (_dataQueries.ContainsKey(entityType))
            {
                _entityDelete?.Invoke(new EntityEventArgs(entity));

                if (entry.State == EntityEntry.States.Deleted)
                {
                    entry.OnCommit(entity);

                    if (entry.State == EntityEntry.States.Deleted)
                    {
                        var ds = _dataQueries[entityType];
                        GetCommandMethod(ds.GetType(), "Delete").Invoke(ds, new[] { entity });

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
            var entityType = _getEntityType(entity);
            if (_dataQueries.ContainsKey(entityType))
            {
                _entityDelete?.Invoke(new EntityEventArgs(entity));

                if (entry.State == EntityEntry.States.Deleted)
                {
                    entry.OnCommit(entity);

                    if (entry.State == EntityEntry.States.Deleted)
                    {
                        var ds = _dataQueries[entityType];
                        await ((Task)GetCommandMethod(ds.GetType(), "DeleteAsync").Invoke(ds, new[] { entity })).ConfigureAwait(false);

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

        private MethodInfo GetCommandMethod(Type dataQueryType, string methodName)
        {
            return _methodCache.GetMethod(dataQueryType, methodName, BindingFlags.Instance | BindingFlags.Public);
        }
    }
}