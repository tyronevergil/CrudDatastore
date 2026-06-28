using System;
using System.Threading.Tasks;

namespace CrudDatastore
{
    public interface IUnitOfWork : IQueryUnit, ICommand, IDisposable
    {
        event EventHandler<EntityEventArgs> EntityCreate;
        event EventHandler<EntityEventArgs> EntityUpdate;
        event EventHandler<EntityEventArgs> EntityDelete;

        void MarkNew<T>(T entity) where T : EntityBase;
        Task MarkNewAsync<T>(T entity) where T : EntityBase;
        void MarkModified<T>(T entity) where T : EntityBase;
        Task MarkModifiedAsync<T>(T entity) where T : EntityBase;
        void MarkDeleted<T>(T entity) where T : EntityBase;
        Task MarkDeletedAsync<T>(T entity) where T : EntityBase;

        void Commit();
        Task CommitAsync();
    }
}
