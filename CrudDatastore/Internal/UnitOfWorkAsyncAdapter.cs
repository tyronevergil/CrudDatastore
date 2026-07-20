using System;
using System.Threading.Tasks;

namespace CrudDatastore.Internal
{
    /// <summary>
    /// Internal adapter that wraps IUnitOfWorkAsync and adapts it to IUnitOfWork.
    /// Synchronous methods block on async calls using GetAwaiter().GetResult().
    /// </summary>
    internal class UnitOfWorkAsyncAdapter : IUnitOfWork
    {
        private readonly IUnitOfWorkAsync _unitOfWorkAsync;

        public event EventHandler<EntityEventArgs> EntityMaterialized
        {
            add { _unitOfWorkAsync.EntityMaterialized += value; }
            remove { _unitOfWorkAsync.EntityMaterialized -= value; }
        }

        public event EventHandler<EntityEventArgs> EntityCreate
        {
            add { _unitOfWorkAsync.EntityCreate += value; }
            remove { _unitOfWorkAsync.EntityCreate -= value; }
        }

        public event EventHandler<EntityEventArgs> EntityUpdate
        {
            add { _unitOfWorkAsync.EntityUpdate += value; }
            remove { _unitOfWorkAsync.EntityUpdate -= value; }
        }

        public event EventHandler<EntityEventArgs> EntityDelete
        {
            add { _unitOfWorkAsync.EntityDelete += value; }
            remove { _unitOfWorkAsync.EntityDelete -= value; }
        }

        public UnitOfWorkAsyncAdapter(IUnitOfWorkAsync asyncUnitOfWork)
        {
            _unitOfWorkAsync = asyncUnitOfWork ?? throw new ArgumentNullException(nameof(asyncUnitOfWork));
        }

        // Synchronous operations (delegated via async)
        public void Execute(string command, params object[] parameters)
        {
            ExecuteAsync(command, parameters).GetAwaiter().GetResult();
        }

        public IDataQuery<T> Read<T>() where T : EntityBase
        {
            return _unitOfWorkAsync.Read<T>();
        }

        public void MarkNew<T>(T entity) where T : EntityBase
        {
            MarkNewAsync(entity).GetAwaiter().GetResult();
        }

        public void MarkModified<T>(T entity) where T : EntityBase
        {
            MarkModifiedAsync(entity).GetAwaiter().GetResult();
        }

        public void MarkDeleted<T>(T entity) where T : EntityBase
        {
            MarkDeletedAsync(entity).GetAwaiter().GetResult();
        }

        public void Commit()
        {
            CommitAsync().GetAwaiter().GetResult();
        }

        // Asynchronous operations (delegated)
        public Task ExecuteAsync(string command, params object[] parameters)
        {
            return _unitOfWorkAsync.ExecuteAsync(command, parameters);
        }

        public Task MarkNewAsync<T>(T entity) where T : EntityBase
        {
            return _unitOfWorkAsync.MarkNewAsync(entity);
        }

        public Task MarkModifiedAsync<T>(T entity) where T : EntityBase
        {
            return _unitOfWorkAsync.MarkModifiedAsync(entity);
        }

        public Task MarkDeletedAsync<T>(T entity) where T : EntityBase
        {
            return _unitOfWorkAsync.MarkDeletedAsync(entity);
        }

        public Task CommitAsync()
        {
            return _unitOfWorkAsync.CommitAsync();
        }

        public void Dispose()
        {
            _unitOfWorkAsync.Dispose();
        }
    }
}
