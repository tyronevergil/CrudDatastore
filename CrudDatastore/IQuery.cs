using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace CrudDatastore
{
    public interface IQuery<T> : IDisposable where T : EntityBase
    {
        IQueryable<T> Execute(Expression<Func<T, bool>> predicate);
        Task<IQueryable<T>> ExecuteAsync(Expression<Func<T, bool>> predicate);
        IQueryable<T> Execute(string command, params object[] parameters);
        Task<IQueryable<T>> ExecuteAsync(string command, params object[] parameters);
    }
}
