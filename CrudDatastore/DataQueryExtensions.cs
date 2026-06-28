using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace CrudDatastore
{
    public static class DataQueryExtensions
    {
        public static IQueryable<T> Find<T>(this IDataQuery<T> instance, Expression<Func<T, bool>> predicate) where T : EntityBase
        {
            return instance.Find(new Specification<T>(predicate));
        }

        public static async Task<IQueryable<T>> FindAsync<T>(this IDataQuery<T> instance, Expression<Func<T, bool>> predicate) where T : EntityBase
        {
            return await instance.FindAsync(new Specification<T>(predicate));
        }

        public static T FindSingle<T>(this IDataQuery<T> instance, Expression<Func<T, bool>> predicate) where T : EntityBase
        {
            return instance.FindSingle(new Specification<T>(predicate));
        }

        public static async Task<T> FindSingleAsync<T>(this IDataQuery<T> instance, Expression<Func<T, bool>> predicate) where T : EntityBase
        {
            return await instance.FindSingleAsync(new Specification<T>(predicate));
        }
    }
}
