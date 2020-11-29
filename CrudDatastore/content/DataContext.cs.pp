using System;
using System.Linq;
using System.Linq.Expressions;
using CrudDatastore;

namespace $rootnamespace$
{
	public class DataContext : DataContextBase
	{
		private DataContext(IUnitOfWork unitOfWork)
            : base(unitOfWork)
		{
		}
        
        public static DataContext Factory()
        {
            return new DataContext(new UnitOfWorkInMemory());
        }
	}

    public static class DataContextExtention
    {
        public static IQueryable<T> Find<T>(this DataContextBase context, Expression<Func<T, bool>> predicate) where T : EntityBase
        {
            return context.Find(new Specification<T>(predicate));
        }

        public static T FindSingle<T>(this DataContextBase context, Expression<Func<T, bool>> predicate) where T : EntityBase
        {
            return context.FindSingle(new Specification<T>(predicate));
        }

        public static void Execute(this DataContextBase context, string sql, params object[] parameters)
        {
            context.Execute(new Command(sql, parameters));
        }
    }
}
