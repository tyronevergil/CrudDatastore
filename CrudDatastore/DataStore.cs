using System;
using System.Linq;

namespace CrudDatastore
{
	public class DataStore<T> : DataQuery<T>, IDataStore<T>, ICrud<T> where T : EntityBase
	{
		private readonly ICrud<T> _crud;

        public DataStore(ICrud<T> crud)
            : this(crud, null)
        { }

        internal DataStore(ICrud<T> crud, Func<T, T> materializeObject)
			: base(crud.Read(), materializeObject)
		{
			_crud = crud;
        }

        public void Add(T entity)
        {
            ((ICrud<T>)this).Create(entity);
        }

        public void Update(T entity)
        {
            ((ICrud<T>)this).Update(entity);
        }

        public void Delete(T entity)
        {
            ((ICrud<T>)this).Delete(entity);
        }

        void ICrud<T>.Create(T entity)
        {
            _crud.Create(entity);
        }

        IQuery<T> ICrud<T>.Read()
        {
            return _crud.Read();
        }

        void ICrud<T>.Update(T entity)
        {
            _crud.Update(entity);
        }

        void ICrud<T>.Delete(T entity)
        {
            _crud.Delete(entity);
        }
    }
}
