using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace CrudDatastore
{
    public class Specification<T> : ISpecification<T> where T : EntityBase
    {
        internal readonly ISpecification<T> _specification;

        public Specification(Expression<Func<T, bool>> predicate)
        {
            _specification = new Internal.SpecificationExpression<T>(predicate);
        }

        public Specification(string command, params object[] parameters)
        {
            _specification = new Internal.SpecificationCommand<T>(command, parameters);
        }

        public IQueryable<T> SatisfyingEntitiesFrom(IQuery<T> query)
        {
            return _specification.SatisfyingEntitiesFrom(query);
        }

        public async Task<IQueryable<T>> SatisfyingEntitiesFromAsync(IQuery<T> query)
        {
            return await _specification.SatisfyingEntitiesFromAsync(query);
        }

        public static implicit operator Func<T, bool>(Specification<T> specification)
        {
            return ((Expression<Func<T, bool>>)specification).Compile();
        }

        public static implicit operator Expression<Func<T, bool>>(Specification<T> specification)
        {
            if (specification._specification is Internal.SpecificationExpression<T> specs)
                return specs._predicate;
            else
                return (e) => false;
        }
    }
}
