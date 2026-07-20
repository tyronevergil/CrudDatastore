using System;
using System.Threading.Tasks;

namespace CrudDatastore.Internal
{
    /// <summary>
    /// Internal adapter that wraps IQueryUnitAsync and adapts it to IQueryUnit.
    /// Synchronous methods block on async calls using GetAwaiter().GetResult().
    /// </summary>
    internal class QueryUnitAsyncAdapter : IQueryUnit
    {
        private readonly IQueryUnitAsync _queryUnitAsync;

        public event EventHandler<EntityEventArgs> EntityMaterialized
        {
            add { _queryUnitAsync.EntityMaterialized += value; }
            remove { _queryUnitAsync.EntityMaterialized -= value; }
        }

        public QueryUnitAsyncAdapter(IQueryUnitAsync queryUnitAsync)
        {
            _queryUnitAsync = queryUnitAsync ?? throw new ArgumentNullException(nameof(queryUnitAsync));
        }

        // Synchronous operations (delegated via async)
        public void Execute(string command, params object[] parameters)
        {
            ExecuteAsync(command, parameters).GetAwaiter().GetResult();
        }

        public IDataQuery<T> Read<T>() where T : EntityBase
        {
            return _queryUnitAsync.Read<T>();
        }

        // Asynchronous operations (delegated)
        public Task ExecuteAsync(string command, params object[] parameters)
        {
            return _queryUnitAsync.ExecuteAsync(command, parameters);
        }

        public void Dispose()
        {
            _queryUnitAsync.Dispose();
        }
    }
}
