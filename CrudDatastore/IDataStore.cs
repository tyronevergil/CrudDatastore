using System;
using System.Threading.Tasks;

namespace CrudDatastore
{
    public interface IDataStore : IDataQuery
    {
    }

    public interface IDataStore<T> : IDataQuery<T>, IDataStore where T : EntityBase
    {
        void Add(T entity);
        Task AddAsync(T entity);
        void Update(T entity);
        Task UpdateAsync(T entity);
        void Delete(T entity);
        Task DeleteAsync(T entity);
    }
}
