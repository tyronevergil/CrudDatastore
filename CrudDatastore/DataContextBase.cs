using System;
using System.Linq;
using System.Threading.Tasks;

namespace CrudDatastore
{
    public abstract class DataContextBase : IDataContext, IDisposable
    {
        private bool _disposed;
        private readonly IUnitOfWork _unitOfWork;

        public DataContextBase(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
            _unitOfWork.EntityMaterialized += (sender, args) => OnEntityMaterialized(args.Entity);
            _unitOfWork.EntityCreate += (sender, args) => OnEntityCreate(args.Entity);
            _unitOfWork.EntityUpdate += (sender, args) => OnEntityUpdate(args.Entity);
            _unitOfWork.EntityDelete += (sender, args) => OnEntityDelete(args.Entity);
        }

        protected virtual void OnEntityMaterialized(object entity)
        {
        }

        protected virtual void OnEntityCreate(object entity)
        {
        }

        protected virtual void OnEntityUpdate(object entity)
        {
        }

        protected virtual void OnEntityDelete(object entity)
        {
        }

        public virtual IQueryable<T> Find<T>(ISpecification<T> specification) where T : EntityBase
        {
            return _unitOfWork.Read<T>().Find(specification);
        }

        public virtual Task<IQueryable<T>> FindAsync<T>(ISpecification<T> specification) where T : EntityBase
        {
            return _unitOfWork.Read<T>().FindAsync(specification);
        }

        public virtual T FindSingle<T>(ISpecification<T> specification) where T : EntityBase
        {
            return _unitOfWork.Read<T>().FindSingle(specification);
        }

        public virtual Task<T> FindSingleAsync<T>(ISpecification<T> specification) where T : EntityBase
        {
            return _unitOfWork.Read<T>().FindSingleAsync(specification);
        }

        public virtual void Add<T>(T entity) where T : EntityBase
        {
            _unitOfWork.MarkNew(entity);
        }

        public virtual Task AddAsync<T>(T entity) where T : EntityBase
        {
            return _unitOfWork.MarkNewAsync(entity);
        }

        public virtual void Update<T>(T entity) where T : EntityBase
        {
            _unitOfWork.MarkModified(entity);
        }

        public virtual Task UpdateAsync<T>(T entity) where T : EntityBase
        {
            return _unitOfWork.MarkModifiedAsync(entity);
        }

        public virtual void Delete<T>(T entity) where T : EntityBase
        {
            _unitOfWork.MarkDeleted(entity);
        }

        public virtual Task DeleteAsync<T>(T entity) where T : EntityBase
        {
            return _unitOfWork.MarkDeletedAsync(entity);
        }

        public virtual void SaveChanges()
        {
            _unitOfWork.Commit();
        }

        public virtual Task SaveChangesAsync()
        {
            return _unitOfWork.CommitAsync();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Free other state (managed objects)
                }

                // Free your own state
                _unitOfWork.Dispose();

                //
                _disposed = true;
            }
        }

        public virtual void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~DataContextBase()
        {
            Dispose(false);
        }
    }
}
