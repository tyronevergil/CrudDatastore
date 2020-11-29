using System;
using System.Linq;

namespace CrudDatastore
{
    public interface IDataContext
    {
        IQueryable<T> Find<T>(ISpecification<T> specification) where T : EntityBase;
        T FindSingle<T>(ISpecification<T> specification) where T : EntityBase;

        void Add<T>(T entity) where T : EntityBase;
        void Update<T>(T entity) where T : EntityBase;
        void Delete<T>(T entity) where T : EntityBase;

        void SaveChanges();
    }
}
