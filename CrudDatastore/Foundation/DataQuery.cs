using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace CrudDatastore.Foundation
{
    public class DataQuery<T> : IDataQuery<T>, IQuery<T> where T : EntityBase
    {
        private bool _disposed;
        private readonly IQuery<T> _query;

        public DataQuery(IQuery<T> query)
        {
            _query = query;
        }

        public virtual IQueryable<T> Find(ISpecification<T> specification)
        {
            return specification.SatisfyingEntitiesFrom(this);
        }

        public virtual async Task<IQueryable<T>> FindAsync(ISpecification<T> specification)
        {
            return await specification.SatisfyingEntitiesFromAsync(this);
        }

        public virtual T FindSingle(ISpecification<T> specification)
        {
            return Find(specification).FirstOrDefault();
        }

        public virtual async Task<T> FindSingleAsync(ISpecification<T> specification)
        {
            return (await FindAsync(specification)).FirstOrDefault();
        }

        IQueryable<T> IQuery<T>.Execute(Expression<Func<T, bool>> predicate)
        {
            return _query.Execute(predicate);
        }

        async Task<IQueryable<T>> IQuery<T>.ExecuteAsync(Expression<Func<T, bool>> predicate)
        {
            return await _query.ExecuteAsync(predicate);
        }

        IQueryable<T> IQuery<T>.Execute(string command, params object[] parameters)
        {
            return _query.Execute(command, parameters);
        }

        async Task<IQueryable<T>> IQuery<T>.ExecuteAsync(string command, params object[] parameters)
        {
            return await _query.ExecuteAsync(command, parameters);
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
                _query.Dispose();

                //
                _disposed = true;
            }
        }

        public virtual void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~DataQuery()
        {
            Dispose(false);
        }
    }
}
