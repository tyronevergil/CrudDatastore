using System;
using System.Linq;
using System.Threading.Tasks;

namespace CrudDatastore
{
    public interface IReadContext
    {
        IQueryable<T> Find<T>(ISpecification<T> specification) where T : EntityBase;
        Task<IQueryable<T>> FindAsync<T>(ISpecification<T> specification) where T : EntityBase;
        T FindSingle<T>(ISpecification<T> specification) where T : EntityBase;
        Task<T> FindSingleAsync<T>(ISpecification<T> specification) where T : EntityBase;
    }
}
