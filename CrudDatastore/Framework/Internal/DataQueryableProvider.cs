using System;
using System.Collections;
using System.Linq;
using System.Linq.Expressions;

namespace CrudDatastore.Framework.Internal
{
    internal class DataQueryableProvider : IQueryProvider
    {
        private readonly Func<IQueryable> _dataFactory;
        private readonly Func<object, object> _materializeObject;

        public DataQueryableProvider(Func<IQueryable> dataFactory, Func<object, object> materializeObject)
        {
            _dataFactory = dataFactory;
            _materializeObject = materializeObject;
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            return new DataQueryable<TElement>(this, expression);
        }

        public IQueryable CreateQuery(Expression expression)
        {
            throw new NotImplementedException();
        }

        public TResult Execute<TResult>(Expression expression)
        {
            var queryableElements = _dataFactory();
            var modifiedExpressionTree = DataQueryableExpressionTreeModifier.CopyAndModify(expression, queryableElements);
            
            var isEnumerable = typeof(IEnumerable).IsAssignableFrom(typeof(TResult)) && Type.GetTypeCode(typeof(TResult)) != TypeCode.String;
            if (isEnumerable)
            {
                var elementType = typeof(TResult).GetGenericArguments().FirstOrDefault();
                if (typeof(EntityBase).IsAssignableFrom(elementType))
                {
                    modifiedExpressionTree = MaterializeObjectExpressionTreeModifier.CopyAndModify(modifiedExpressionTree, _materializeObject);
                }

                return queryableElements.Provider.Execute<TResult>(modifiedExpressionTree);
            }
            else
            {
                if (typeof(EntityBase).IsAssignableFrom(expression.Type))
                    return (TResult)_materializeObject(queryableElements.Provider.Execute<TResult>(modifiedExpressionTree));
                else
                    return queryableElements.Provider.Execute<TResult>(modifiedExpressionTree);
            }
        }

        public object Execute(Expression expression)
        {
            throw new NotImplementedException();
        }
    }
}
