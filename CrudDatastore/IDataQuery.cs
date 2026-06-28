using System;
using System.Linq;
using System.Threading.Tasks;

namespace CrudDatastore
{
    public interface IDataQuery : IDisposable
    {
    }

    public interface IDataQuery<T> : IDataQuery where T : EntityBase
    {
        IQueryable<T> Find(ISpecification<T> specification);
        Task<IQueryable<T>> FindAsync(ISpecification<T> specification);
        T FindSingle(ISpecification<T> specification);
        Task<T> FindSingleAsync(ISpecification<T> specification);
    }
}
