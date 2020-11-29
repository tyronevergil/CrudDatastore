using System;
using System.Linq;
using System.Linq.Expressions;

namespace CrudDatastore
{
	public class Specification<T> : ISpecification<T> where T : EntityBase
	{
		internal readonly ISpecification<T> _specification;

		public Specification(Expression<Func<T, bool>> predicate)
		{
			_specification = new SpecificationExpression<T>(predicate);
		}

		public Specification(string command, params object[] parameters)
		{
			_specification = new SpecificationCommand<T>(command, parameters);
		}

		public IQueryable<T> SatisfyingEntitiesFrom(IQuery<T> query)
		{
			return _specification.SatisfyingEntitiesFrom(query);
		}

        public static implicit operator Func<T, bool>(Specification<T> specification)
        {
            return ((Expression<Func<T, bool>>)specification).Compile();
        }

        public static implicit operator Expression<Func<T, bool>>(Specification<T> specification)
        {
            if (specification._specification is SpecificationExpression<T> specs)
                return specs._predicate;
            else
                return (e) => false;
        }
	}

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
	}

	internal class SpecificationCommand<T> : ISpecification<T> where T : EntityBase
	{
		private readonly string _command;
		private readonly object[] _parameters;

		public SpecificationCommand(string command, object[] parameters)
		{
			_command = command;
			_parameters = parameters;
		}

		public IQueryable<T> SatisfyingEntitiesFrom(IQuery<T> query)
		{
			return query.Execute(_command, _parameters);
		}
	}
}
