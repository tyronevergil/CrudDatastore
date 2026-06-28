using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace CrudDatastore
{
    public static class ReadContextExtensions
    {
        public static IQueryable<T> Find<T>(this IReadContext instance, Expression<Func<T, bool>> predicate) where T : EntityBase
        {
            return instance.Find(new Specification<T>(predicate));
        }

        public static async Task<IQueryable<T>> FindAsync<T>(this IReadContext instance, Expression<Func<T, bool>> predicate) where T : EntityBase
        {
            return await instance.FindAsync(new Specification<T>(predicate));
        }

        public static T FindSingle<T>(this IReadContext instance, Expression<Func<T, bool>> predicate) where T : EntityBase
        {
            return instance.FindSingle(new Specification<T>(predicate));
        }

        public static async Task<T> FindSingleAsync<T>(this IReadContext instance, Expression<Func<T, bool>> predicate) where T : EntityBase
        {
            return await instance.FindSingleAsync(new Specification<T>(predicate));
        }
    }
}
