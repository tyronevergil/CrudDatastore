using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace CrudDatastore.Foundation
{
    public class DelegateCrudAdapter<T> : DelegateQueryAdapter<T>, ICrud<T> where T : EntityBase
    {
        private readonly Action<T> _createTrigger;
        private readonly Action<T> _updateTrigger;
        private readonly Action<T> _deleteTrigger;

        private readonly Func<T, Task> _createTriggerAsync;
        private readonly Func<T, Task> _updateTriggerAsync;
        private readonly Func<T, Task> _deleteTriggerAsync;

        public DelegateCrudAdapter(Action<T> createTrigger,
                                   Action<T> updateTrigger,
                                   Action<T> deleteTrigger,
                                   Func<Expression<Func<T, bool>>, IQueryable<T>> readExpressionTrigger)
            : this(createTrigger, updateTrigger, deleteTrigger, readExpressionTrigger, (sql, parameters) => readExpressionTrigger((predicate) => false))
        {
        }

        public DelegateCrudAdapter(Action<T> createTrigger,
                                   Action<T> updateTrigger,
                                   Action<T> deleteTrigger,
                                   Func<Expression<Func<T, bool>>, IQueryable<T>> readExpressionTrigger,
                                   Func<string, object[], IQueryable<T>> readCommandTrigger)
            : base(readExpressionTrigger, readCommandTrigger)
        {
            _createTrigger = createTrigger;
            _updateTrigger = updateTrigger;
            _deleteTrigger = deleteTrigger;

            _createTriggerAsync = (entity) => Task.Run(() => _createTrigger(entity));
            _updateTriggerAsync = (entity) => Task.Run(() => _updateTrigger(entity));
            _deleteTriggerAsync = (entity) => Task.Run(() => _deleteTrigger(entity));
        }

        public DelegateCrudAdapter(Func<T, Task> createTriggerAsync,
                                   Func<T, Task> updateTriggerAsync,
                                   Func<T, Task> deleteTriggerAsync,
                                   Func<Expression<Func<T, bool>>, Task<IQueryable<T>>> readExpressionTriggerAsync)
            : this(createTriggerAsync, updateTriggerAsync, deleteTriggerAsync, readExpressionTriggerAsync, (sql, parameters) => readExpressionTriggerAsync((predicate) => false))
        {
        }

        public DelegateCrudAdapter(Func<T, Task> createTriggerAsync,
                                   Func<T, Task> updateTriggerAsync,
                                   Func<T, Task> deleteTriggerAsync,
                                   Func<Expression<Func<T, bool>>, Task<IQueryable<T>>> readExpressionTriggerAsync,
                                   Func<string, object[], Task<IQueryable<T>>> readCommandTriggerAsync)
            : base(readExpressionTriggerAsync, readCommandTriggerAsync)
        {
            _createTriggerAsync = createTriggerAsync;
            _updateTriggerAsync = updateTriggerAsync;
            _deleteTriggerAsync = deleteTriggerAsync;
        }

        public DelegateCrudAdapter(Action<T> createTrigger,
                                   Action<T> updateTrigger,
                                   Action<T> deleteTrigger,
                                   Func<T, Task> createTriggerAsync,
                                   Func<T, Task> updateTriggerAsync,
                                   Func<T, Task> deleteTriggerAsync,
                                   Func<Expression<Func<T, bool>>, IQueryable<T>> readExpressionTrigger,
                                   Func<Expression<Func<T, bool>>, Task<IQueryable<T>>> readExpressionTriggerAsync)
            : this(createTrigger, updateTrigger, deleteTrigger,
                   createTriggerAsync, updateTriggerAsync, deleteTriggerAsync,
                   readExpressionTrigger, (sql, parameters) => readExpressionTrigger((predicate) => false),
                   readExpressionTriggerAsync, (sql, parameters) => readExpressionTriggerAsync((predicate) => false))
        {
        }

        public DelegateCrudAdapter(Action<T> createTrigger,
                                   Action<T> updateTrigger,
                                   Action<T> deleteTrigger,
                                   Func<T, Task> createTriggerAsync,
                                   Func<T, Task> updateTriggerAsync,
                                   Func<T, Task> deleteTriggerAsync,
                                   Func<Expression<Func<T, bool>>, IQueryable<T>> readExpressionTrigger,
                                   Func<string, object[], IQueryable<T>> readCommandTrigger,
                                   Func<Expression<Func<T, bool>>, Task<IQueryable<T>>> readExpressionTriggerAsync,
                                   Func<string, object[], Task<IQueryable<T>>> readCommandTriggerAsync)
            : base(readExpressionTrigger, readCommandTrigger, readExpressionTriggerAsync, readCommandTriggerAsync)
        {
            _createTrigger = createTrigger;
            _updateTrigger = updateTrigger;
            _deleteTrigger = deleteTrigger;
            _createTriggerAsync = createTriggerAsync;
            _updateTriggerAsync = updateTriggerAsync;
            _deleteTriggerAsync = deleteTriggerAsync;
        }

        public virtual void Create(T entity)
        {
            if (_createTrigger == null)
            {
                throw new NotImplementedException();
            }

            _createTrigger(entity);
        }

        public virtual Task CreateAsync(T entity)
        {
            return _createTriggerAsync(entity);
        }

        public virtual void Update(T entity)
        {
            if (_updateTrigger == null)
            {
                throw new NotImplementedException();
            }

            _updateTrigger(entity);
        }

        public virtual Task UpdateAsync(T entity)
        {
            return _updateTriggerAsync(entity);
        }

        public virtual void Delete(T entity)
        {
            if (_deleteTrigger == null)
            {
                throw new NotImplementedException();
            }

            _deleteTrigger(entity);
        }

        public virtual Task DeleteAsync(T entity)
        {
            return _deleteTriggerAsync(entity);
        }

        public virtual IQuery<T> Read()
        {
            return this;
        }
    }
}
