using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace CrudDatastore.Foundation
{
    public class DelegateQueryAdapter<T> : IQuery<T> where T : EntityBase
    {
        private readonly Func<Expression<Func<T, bool>>, IQueryable<T>> _readExpressionTrigger;
        private readonly Func<string, object[], IQueryable<T>> _readCommandTrigger;

        private readonly Func<Expression<Func<T, bool>>, Task<IQueryable<T>>> _readExpressionTriggerAsync;
        private readonly Func<string, object[], Task<IQueryable<T>>> _readCommandTriggerAsync;

        public DelegateQueryAdapter(Func<Expression<Func<T, bool>>, IQueryable<T>> readExpressionTrigger)
            : this(readExpressionTrigger, (sql, parameters) => readExpressionTrigger((predicate) => false))
        {
        }

        public DelegateQueryAdapter(Func<Expression<Func<T, bool>>, IQueryable<T>> readExpressionTrigger, Func<string, object[], IQueryable<T>> readCommandTrigger)
        {
            _readExpressionTrigger = readExpressionTrigger;
            _readCommandTrigger = readCommandTrigger;

            _readExpressionTriggerAsync = (predicate) => Task.Run(() => _readExpressionTrigger(predicate));
            _readCommandTriggerAsync = (sql, parameters) => Task.Run(() => _readCommandTrigger(sql, parameters));
        }

        public DelegateQueryAdapter(Func<Expression<Func<T, bool>>, Task<IQueryable<T>>> readExpressionTriggerAsync)
            : this(readExpressionTriggerAsync, (sql, parameters) => readExpressionTriggerAsync((predicate) => false))
        {
        }

        public DelegateQueryAdapter(Func<Expression<Func<T, bool>>, Task<IQueryable<T>>> readExpressionTriggerAsync, Func<string, object[], Task<IQueryable<T>>> readCommandTriggerAsync)
        {
            _readExpressionTriggerAsync = readExpressionTriggerAsync;
            _readCommandTriggerAsync = readCommandTriggerAsync;
        }

        public DelegateQueryAdapter(Func<Expression<Func<T, bool>>, IQueryable<T>> readExpressionTrigger,
                                    Func<Expression<Func<T, bool>>, Task<IQueryable<T>>> readExpressionTriggerAsync)
            : this(readExpressionTrigger, (sql, parameters) => readExpressionTrigger((predicate) => false),
                   readExpressionTriggerAsync, (sql, parameters) => readExpressionTriggerAsync((predicate) => false))
        {
        }

        public DelegateQueryAdapter(Func<Expression<Func<T, bool>>, IQueryable<T>> readExpressionTrigger,
                                    Func<string, object[], IQueryable<T>> readCommandTrigger,
                                    Func<Expression<Func<T, bool>>, Task<IQueryable<T>>> readExpressionTriggerAsync,
                                    Func<string, object[], Task<IQueryable<T>>> readCommandTriggerAsync)
        {
            _readExpressionTrigger = readExpressionTrigger;
            _readCommandTrigger = readCommandTrigger;
            _readExpressionTriggerAsync = readExpressionTriggerAsync;
            _readCommandTriggerAsync = readCommandTriggerAsync;
        }

        public virtual IQueryable<T> Execute(Expression<Func<T, bool>> predicate)
        {
            if (_readExpressionTrigger == null)
            {
                throw new NotImplementedException();
            }

            return _readExpressionTrigger(predicate);
        }

        public virtual Task<IQueryable<T>> ExecuteAsync(Expression<Func<T, bool>> predicate)
        {
            return _readExpressionTriggerAsync(predicate);
        }

        public virtual IQueryable<T> Execute(string sql, params object[] parameters)
        {
            if (_readCommandTrigger == null)
            {
                throw new NotImplementedException();
            }

            return _readCommandTrigger(sql, parameters);
        }

        public virtual Task<IQueryable<T>> ExecuteAsync(string sql, params object[] parameters)
        {
            return _readCommandTriggerAsync(sql, parameters);
        }

        public virtual void Dispose()
        {
        }
    }
}
