using System;
using System.Linq;
using System.Threading.Tasks;
using CrudDatastore.Internal;

namespace CrudDatastore
{
    public abstract class ReadContextBase : IReadContext, ICommandContext, IDisposable
    {
        private bool _disposed;
        private readonly IQueryUnit _queryUnit;

        public ReadContextBase(IQueryUnitSync queryUnitSync)
            : this(new QueryUnitSyncAdapter(queryUnitSync))
        { }

        public ReadContextBase(IQueryUnitAsync queryUnitAsync)
            : this(new QueryUnitAsyncAdapter(queryUnitAsync))
        { }

        public ReadContextBase(IQueryUnit queryUnit)
        {
            _queryUnit = queryUnit;
            _queryUnit.EntityMaterialized += (sender, args) => OnEntityMaterialized(args.Entity);
        }

        protected virtual void OnEntityMaterialized(object entity)
        {
        }

        public IQueryable<T> Find<T>(ISpecification<T> specification) where T : EntityBase
        {
            return _queryUnit.Read<T>().Find(specification);
        }

        public Task<IQueryable<T>> FindAsync<T>(ISpecification<T> specification) where T : EntityBase
        {
            return _queryUnit.Read<T>().FindAsync(specification);
        }

        public T FindSingle<T>(ISpecification<T> specification) where T : EntityBase
        {
            return _queryUnit.Read<T>().FindSingle(specification);
        }

        public Task<T> FindSingleAsync<T>(ISpecification<T> specification) where T : EntityBase
        {
            return _queryUnit.Read<T>().FindSingleAsync(specification);
        }

        public void Execute(IAction action)
        {
            action.SatisfyingActionFrom((ICommand)_queryUnit);
        }

        public Task ExecuteAsync(IAction action)
        {
            return action.SatisfyingActionFromAsync((ICommand)_queryUnit);
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
                _queryUnit.Dispose();

                //
                _disposed = true;
            }
        }

        public virtual void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~ReadContextBase()
        {
            Dispose(false);
        }
    }
}
