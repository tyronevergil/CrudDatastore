using System;
using System.Linq;
using System.Threading.Tasks;

namespace CrudDatastore
{
    public interface ISpecification<T> where T : EntityBase
    {
        IQueryable<T> SatisfyingEntitiesFrom(IQuery<T> query);
        Task<IQueryable<T>> SatisfyingEntitiesFromAsync(IQuery<T> query);
    }
}
