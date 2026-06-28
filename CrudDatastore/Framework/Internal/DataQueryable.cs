using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace CrudDatastore.Framework.Internal
{
    internal class DataQueryable<T> : IOrderedQueryable<T>, IDataQueryable
    {
        private readonly Func<IQueryable<T>> _dataFactory;
        private readonly Func<T, T> _materializeObject;

        public DataQueryable(Func<IQueryable<T>> dataFactory, Func<T, T> materializeObject)
        {
            _dataFactory = dataFactory;
            _materializeObject = materializeObject;

            Provider = new DataQueryableProvider(_dataFactory, (entity) => _materializeObject((T)entity));
            Expression = Expression.Constant(this);
        }

        public DataQueryable(IQueryProvider provider, Expression expression)
        {
            Provider = provider;
            Expression = expression;
        }

        public Expression Expression { get; private set; }

        public IQueryProvider Provider { get; private set; }

        public IEnumerator<T> GetEnumerator()
        {
            var enumerator = Provider.Execute<IEnumerable<T>>(Expression).GetEnumerator();
            return new MaterializeObjectEnumerator<T>(enumerator, _materializeObject);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }

        public Type ElementType
        {
            get { return typeof(T); }
        }
    }
}
