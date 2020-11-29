using System;
using System.Linq;
using System.Linq.Expressions;

namespace CrudDatastore
{
	public interface IQuery<T> where T : EntityBase
	{
		IQueryable<T> Execute(Expression<Func<T, bool>> predicate);
		IQueryable<T> Execute(string command, params object[] parameters);
	}
}
