using System;
using System.Threading.Tasks;

namespace CrudDatastore.Framework
{
    public class DataStore<T> : DataQuery<T>, IDataStore<T>, ICrud<T> where T : EntityBase
    {
        private bool _disposed;
        private readonly ICrud<T> _crud;

        public DataStore(ICrud<T> crud)
            : base(crud.Read())
        {
            _crud = crud;
        }

        protected DataStore(ICrud<T> crud, Func<T, T> materializeObject)
            : base(crud.Read(), materializeObject)
        {
            _crud = crud;
        }

        public void Add(T entity)
        {
            ((ICrud<T>)this).Create(entity);
        }

        public Task AddAsync(T entity)
        {
            return ((ICrud<T>)this).CreateAsync(entity);
        }

        public void Update(T entity)
        {
            ((ICrud<T>)this).Update(entity);
        }

        public Task UpdateAsync(T entity)
        {
            return ((ICrud<T>)this).UpdateAsync(entity);
        }

        public void Delete(T entity)
        {
            ((ICrud<T>)this).Delete(entity);
        }

        public Task DeleteAsync(T entity)
        {
            return ((ICrud<T>)this).DeleteAsync(entity);
        }

        void ICrud<T>.Create(T entity)
        {
            _crud.Create(entity);
        }

        Task ICrud<T>.CreateAsync(T entity)
        {
            return _crud.CreateAsync(entity);
        }

        void ICrud<T>.Update(T entity)
        {
            _crud.Update(entity);
        }

        Task ICrud<T>.UpdateAsync(T entity)
        {
            return _crud.UpdateAsync(entity);
        }

        void ICrud<T>.Delete(T entity)
        {
            _crud.Delete(entity);
        }

        Task ICrud<T>.DeleteAsync(T entity)
        {
            return _crud.DeleteAsync(entity);
        }

        IQuery<T> ICrud<T>.Read()
        {
            return _crud.Read();
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Free other state (managed objects)
                }

                // Free your own state
                _crud.Dispose();

                //
                _disposed = true;
            }

            base.Dispose(disposing);
        }

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);

            base.Dispose();
        }

        ~DataStore()
        {
            Dispose(false);
        }
    }
}
