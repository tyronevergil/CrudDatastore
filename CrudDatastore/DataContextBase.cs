using System;
using System.Linq;

namespace CrudDatastore
{
    public abstract class DataContextBase : IDataContext, IDisposable
    {
        private bool _disposed;
        private readonly IUnitOfWork _unitOfWork;

        public DataContextBase(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
            _unitOfWork.EntityMaterialized += (sender, e) => OnEntityMaterialized(e.Entity);
            _unitOfWork.EntityCreate += (sender, e) => OnEntityCreate(e.Entity);
            _unitOfWork.EntityUpdate += (sender, e) => OnEntityUpdate(e.Entity);
            _unitOfWork.EntityDelete += (sender, e) => OnEntityDelete(e.Entity);
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

        public virtual T FindSingle<T>(ISpecification<T> specification) where T : EntityBase
        {
            return _unitOfWork.Read<T>().FindSingle(specification);
        }

        public virtual void Add<T>(T entity) where T : EntityBase
        {
            _unitOfWork.MarkNew(entity);
        }

        public virtual void Update<T>(T entity) where T : EntityBase
        {
            _unitOfWork.MarkModified(entity);
        }

        public virtual void Delete<T>(T entity) where T : EntityBase
        {
            _unitOfWork.MarkDeleted(entity);
        }

        public virtual void SaveChanges()
        {
            _unitOfWork.Commit();
        }

        public virtual void Execute(ICommand command)
        {
            command.SatisfyingFrom(_unitOfWork);
        }

        protected IEntityEntry Entry(object entity)
        {
            return new EntityEntryModifier(entity, _unitOfWork);
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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~DataContextBase()
        {
            Dispose(false);
        }
    }

    public interface IEntityEntry
    {
        void MarkNew();
        void MarkModified();
        void MarkDeleted();
    }

    internal class EntityEntryModifier : IEntityEntry
    {
        private readonly object _entity;
        private readonly IUnitOfWork _unitOfWork;

        public EntityEntryModifier(object entity, IUnitOfWork unitOfWork)
        {
            _entity = entity;
            _unitOfWork = unitOfWork;
        }

        public void MarkNew()
        {
            var method = typeof(IUnitOfWork).GetMethod("MarkNew");
            var genericMethod = method.MakeGenericMethod(_entity.GetType());
            genericMethod.Invoke(_unitOfWork, new[] { _entity });
        }

        public void MarkModified()
        {
            var method = typeof(IUnitOfWork).GetMethod("MarkModified");
            var genericMethod = method.MakeGenericMethod(_entity.GetType());
            genericMethod.Invoke(_unitOfWork, new[] { _entity });
        }

        public void MarkDeleted()
        {
            var method = typeof(IUnitOfWork).GetMethod("MarkDeleted");
            var genericMethod = method.MakeGenericMethod(_entity.GetType());
            genericMethod.Invoke(_unitOfWork, new[] { _entity });
        }
    }
}
