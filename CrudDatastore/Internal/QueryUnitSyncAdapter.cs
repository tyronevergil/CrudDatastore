using System;
using System.Threading.Tasks;

namespace CrudDatastore.Internal
{
    /// <summary>
    /// Internal adapter that wraps IQueryUnitSync and adapts it to IQueryUnit.
    /// Async methods wrap sync calls and return Task.CompletedTask.
    /// </summary>
    internal class QueryUnitSyncAdapter : IQueryUnit
    {
        private readonly IQueryUnitSync _queryUnitSync;

        public event EventHandler<EntityEventArgs> EntityMaterialized
        {
            add { _queryUnitSync.EntityMaterialized += value; }
            remove { _queryUnitSync.EntityMaterialized -= value; }
        }

        public QueryUnitSyncAdapter(IQueryUnitSync queryUnitSync)
        {
            _queryUnitSync = queryUnitSync ?? throw new ArgumentNullException(nameof(queryUnitSync));
        }

        // Synchronous operations (delegated)
        public void Execute(string command, params object[] parameters)
        {
            _queryUnitSync.Execute(command, parameters);
        }

        public IDataQuery<T> Read<T>() where T : EntityBase
        {
            return _queryUnitSync.Read<T>();
        }

        // Asynchronous operations (delegated via sync)
        public Task ExecuteAsync(string command, params object[] parameters)
        {
            Execute(command, parameters);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _queryUnitSync.Dispose();
        }
    }
}
