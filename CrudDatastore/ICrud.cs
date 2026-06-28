using System;
using System.Threading.Tasks;

namespace CrudDatastore
{
    public interface ICrud<T> : IDisposable where T : EntityBase
    {
        void Create(T entity);
        Task CreateAsync(T entity);
        void Update(T entity);
        Task UpdateAsync(T entity);
        void Delete(T entity);
        Task DeleteAsync(T entity);

        IQuery<T> Read();
    }
}
