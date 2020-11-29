using System;

namespace CrudDatastore
{
	public interface ICrud<T> where T : EntityBase
	{
		void Create(T entity);
        IQuery<T> Read();
		void Update(T entity);
		void Delete(T entity);
	}
}
