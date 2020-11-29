using System;
using System.Linq;
using System.Linq.Expressions;

namespace CrudDatastore
{
	public class DelegateQueryAdapter<T> : IQuery<T> where T : EntityBase
	{
		private readonly Func<Expression<Func<T, bool>>, IQueryable<T>> _readExpressionTrigger;
		private readonly Func<string, object[], IQueryable<T>> _readCommandTrigger;

        public DelegateQueryAdapter(Func<Expression<Func<T, bool>>, IQueryable<T>> readExpressionTrigger)
            : this(readExpressionTrigger, (sql, parameters) => readExpressionTrigger((e) => false))
        {
        }

        public DelegateQueryAdapter(Func<Expression<Func<T, bool>>, IQueryable<T>> readExpressionTrigger, Func<string, object[], IQueryable<T>> readCommandTrigger)
		{
			_readExpressionTrigger = readExpressionTrigger;
			_readCommandTrigger = readCommandTrigger;
		}

		public virtual IQueryable<T> Execute(Expression<Func<T, bool>> predicate)
		{
			return _readExpressionTrigger(predicate);
		}

		public virtual IQueryable<T> Execute(string sql, params object[] parameters)
		{
			return _readCommandTrigger(sql, parameters);
		}
	}
}
