using CrudDatastore.Framework.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace CrudDatastore.Framework
{
    public class DataQuery<T> : IDataQuery<T>, IQuery<T> where T : EntityBase
    {
        private bool _disposed;
        private readonly IQuery<T> _query;

        private Func<T, T> _materializeObject;
        private Func<T, Task<T>> _materializeObjectAsync;
        private Func<Expression<Func<T, bool>>, Expression<Func<T, bool>>> _modifierPredicate;

        public DataQuery(IQuery<T> query)
            : this(query, (entity) =>
            {
                if (entity == null)
                    return entity;

                var method = typeof(T).GetMethod(nameof(MemberwiseClone), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                return (T)method.Invoke(entity, null);
            })
        {
        }

        protected DataQuery(IQuery<T> query, Func<T, T> materializeObject)
        {
            _query = query;
            _materializeObject = materializeObject;
            _materializeObjectAsync = (entity) => Task.FromResult(_materializeObject(entity));
        }

        /* This is a temporary hack to replace _materializeObject and _modifierPredicate in QueryUnitBase. */
        protected void SetMaterializationBehavior(Func<T, T> materializeObject, Func<Expression<Func<T, bool>>, Expression<Func<T, bool>>> modifierPredicate)
        {
            _materializeObject = materializeObject;
            _modifierPredicate = modifierPredicate;
        }

        protected void SetAsyncMaterializationBehavior(Func<T, Task<T>> materializeObjectAsync)
        {
            _materializeObjectAsync = materializeObjectAsync ?? ((entity) => Task.FromResult(_materializeObject(entity)));
        }

        public virtual IQueryable<T> Find(ISpecification<T> specification)
        {
            return specification.SatisfyingEntitiesFrom(this);
        }

        public virtual async Task<IQueryable<T>> FindAsync(ISpecification<T> specification)
        {
            var data = await specification.SatisfyingEntitiesFromAsync(this);

            var result = new List<T>();
            foreach (var item in data)
            {
                result.Add(await _materializeObjectAsync(item));
            }

            return result.AsQueryable();
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
            if (_modifierPredicate != null)
            {
                predicate = _modifierPredicate(predicate);
            }

            return new Internal.DataQueryable<T>(() => _query.Execute(predicate), _materializeObject);
        }

        async Task<IQueryable<T>> IQuery<T>.ExecuteAsync(Expression<Func<T, bool>> predicate)
        {
            if (_modifierPredicate != null)
            {
                predicate = _modifierPredicate(predicate);
            }

            return await _query.ExecuteAsync(predicate);
        }

        IQueryable<T> IQuery<T>.Execute(string command, params object[] parameters)
        {
            return new Internal.DataQueryable<T>(() => _query.Execute(command, parameters), _materializeObject);
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
