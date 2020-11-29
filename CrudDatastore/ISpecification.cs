using System;
using System.Linq;

namespace CrudDatastore
{
	public interface ISpecification<T> where T : EntityBase
	{
		IQueryable<T> SatisfyingEntitiesFrom(IQuery<T> query);
	}
}
