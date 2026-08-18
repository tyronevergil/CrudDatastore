using System;
using System.Threading.Tasks;
using CrudDatastore.Internal;

namespace CrudDatastore
{
    public abstract class DataContextBase : QueryContextBase, IDataContext
    {
        private readonly IUnitOfWork _unitOfWork;

        public DataContextBase(IUnitOfWorkSync unitOfWorkSync)
            : this(new UnitOfWorkSyncAdapter(unitOfWorkSync))
        { }

        public DataContextBase(IUnitOfWorkAsync unitOfWorkAsync)
            : this(new UnitOfWorkAsyncAdapter(unitOfWorkAsync))
        { }

        public DataContextBase(IUnitOfWork unitOfWork)
            : base((IQueryUnit)unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _unitOfWork.EntityCreate += (sender, args) => OnEntityCreate(args.Entity);
            _unitOfWork.EntityUpdate += (sender, args) => OnEntityUpdate(args.Entity);
            _unitOfWork.EntityDelete += (sender, args) => OnEntityDelete(args.Entity);
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

    }
}
