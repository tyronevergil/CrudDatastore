using System;
using System.Linq;
using System.Threading.Tasks;

namespace CrudDatastore.Internal
{
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

        public async Task<IQueryable<T>> SatisfyingEntitiesFromAsync(IQuery<T> query)
        {
            return await query.ExecuteAsync(_command, _parameters);
        }
    }
}
