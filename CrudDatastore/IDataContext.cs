using System;
using System.Linq;
using System.Threading.Tasks;

namespace CrudDatastore
{
    public interface IDataContext : IReadContext
    {
        /*
        IQueryable<T> Find<T>(ISpecification<T> specification) where T : EntityBase;
        Task<IQueryable<T>> FindAsync<T>(ISpecification<T> specification) where T : EntityBase;
        T FindSingle<T>(ISpecification<T> specification) where T : EntityBase;
        Task<T> FindSingleAsync<T>(ISpecification<T> specification) where T : EntityBase;
        */
        
        void Add<T>(T entity) where T : EntityBase;
        Task AddAsync<T>(T entity) where T : EntityBase;
        void Update<T>(T entity) where T : EntityBase;
        Task UpdateAsync<T>(T entity) where T : EntityBase;
        void Delete<T>(T entity) where T : EntityBase;
        Task DeleteAsync<T>(T entity) where T : EntityBase;

        void SaveChanges();
        Task SaveChangesAsync();
    }
}
