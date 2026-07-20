using System;
using System.Threading.Tasks;

namespace CrudDatastore
{
    /// <summary>
    /// Asynchronous-only unit of work contract. 
    /// Implement this interface when your unit of work provides only asynchronous operations.
    /// </summary>
    public interface IUnitOfWorkAsync : IQueryUnitAsync, IUnitOfWorkEvents, IDisposable
    {
        Task MarkNewAsync<T>(T entity) where T : EntityBase;
        Task MarkModifiedAsync<T>(T entity) where T : EntityBase;
        Task MarkDeletedAsync<T>(T entity) where T : EntityBase;

        Task CommitAsync();
    }
}
