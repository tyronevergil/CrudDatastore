using System;
using System.Linq;

namespace CrudDatastore
{
    public interface IDataStore : IDataQuery
    {
    }

	public interface IDataStore<T> : IDataQuery<T>, IDataStore where T : EntityBase
	{
		void Add(T entity);
		void Update(T entity);
		void Delete(T entity);
	}
}
