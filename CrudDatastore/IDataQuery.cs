using System;
using System.Linq;

namespace CrudDatastore
{
    public interface IDataQuery
    {
    }

    public interface IDataQuery<T> : IDataQuery where T : EntityBase
	{
		IQueryable<T> Find(ISpecification<T> specification);
        T FindSingle(ISpecification<T> specification);
    }
}
