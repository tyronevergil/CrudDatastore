using System;

namespace CrudDatastore
{
    /// <summary>
    /// Synchronous-only unit of work contract. 
    /// Implement this interface when your unit of work provides only synchronous operations.
    /// </summary>
    public interface IUnitOfWorkSync : IQueryUnitSync, IUnitOfWorkEvents, IDisposable
    {
        void MarkNew<T>(T entity) where T : EntityBase;
        void MarkModified<T>(T entity) where T : EntityBase;
        void MarkDeleted<T>(T entity) where T : EntityBase;

        void Commit();
    }
}
