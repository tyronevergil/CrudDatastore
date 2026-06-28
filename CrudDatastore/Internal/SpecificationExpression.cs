using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace CrudDatastore.Internal
{
    internal class SpecificationExpression<T> : ISpecification<T> where T : EntityBase
    {
        internal readonly Expression<Func<T, bool>> _predicate;

        public SpecificationExpression(Expression<Func<T, bool>> predicate)
        {
            _predicate = predicate;
        }

        public IQueryable<T> SatisfyingEntitiesFrom(IQuery<T> query)
        {
            return query.Execute(_predicate);
        }

        public async Task<IQueryable<T>> SatisfyingEntitiesFromAsync(IQuery<T> query)
        {
            return await query.ExecuteAsync(_predicate);
        }
    }
}
