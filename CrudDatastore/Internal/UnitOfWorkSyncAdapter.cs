using System;
using System.Threading.Tasks;

namespace CrudDatastore.Internal
{
    /// <summary>
    /// Internal adapter that wraps IUnitOfWorkSync and adapts it to IUnitOfWork.
    /// Async methods wrap sync calls and return Task.CompletedTask.
    /// </summary>
    internal class UnitOfWorkSyncAdapter : IUnitOfWork
    {
        private readonly IUnitOfWorkSync _unitOfWorkSync;

        public event EventHandler<EntityEventArgs> EntityMaterialized
        {
            add { _unitOfWorkSync.EntityMaterialized += value; }
            remove { _unitOfWorkSync.EntityMaterialized -= value; }
        }

        public event EventHandler<EntityEventArgs> EntityCreate
        {
            add { _unitOfWorkSync.EntityCreate += value; }
            remove { _unitOfWorkSync.EntityCreate -= value; }
        }

        public event EventHandler<EntityEventArgs> EntityUpdate
        {
            add { _unitOfWorkSync.EntityUpdate += value; }
            remove { _unitOfWorkSync.EntityUpdate -= value; }
        }

        public event EventHandler<EntityEventArgs> EntityDelete
        {
            add { _unitOfWorkSync.EntityDelete += value; }
            remove { _unitOfWorkSync.EntityDelete -= value; }
        }

        public UnitOfWorkSyncAdapter(IUnitOfWorkSync syncUnitOfWork)
        {
            _unitOfWorkSync = syncUnitOfWork ?? throw new ArgumentNullException(nameof(syncUnitOfWork));
        }

        // Synchronous operations (delegated)
        public void Execute(string command, params object[] parameters)
        {
            _unitOfWorkSync.Execute(command, parameters);
        }

        public IDataQuery<T> Read<T>() where T : EntityBase
        {
            return _unitOfWorkSync.Read<T>();
        }

        public void MarkNew<T>(T entity) where T : EntityBase
        {
            _unitOfWorkSync.MarkNew(entity);
        }

        public void MarkModified<T>(T entity) where T : EntityBase
        {
            _unitOfWorkSync.MarkModified(entity);
        }

        public void MarkDeleted<T>(T entity) where T : EntityBase
        {
            _unitOfWorkSync.MarkDeleted(entity);
        }

        public void Commit()
        {
            _unitOfWorkSync.Commit();
        }

        // Asynchronous operations (delegated via sync)
        public Task ExecuteAsync(string command, params object[] parameters)
        {
            Execute(command, parameters);
            return Task.CompletedTask;
        }

        public Task MarkNewAsync<T>(T entity) where T : EntityBase
        {
            MarkNew(entity);
            return Task.CompletedTask;
        }

        public Task MarkModifiedAsync<T>(T entity) where T : EntityBase
        {
            MarkModified(entity);
            return Task.CompletedTask;
        }

        public Task MarkDeletedAsync<T>(T entity) where T : EntityBase
        {
            MarkDeleted(entity);
            return Task.CompletedTask;
        }

        public Task CommitAsync()
        {
            Commit();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _unitOfWorkSync.Dispose();
        }
    }
}
