using System;
using System.Linq;
using System.Linq.Expressions;

namespace CrudDatastore.Framework.Internal
{
    internal class DataQueryableExpressionTreeModifier : ExpressionVisitor
    {
        private readonly IQueryable _queryablePlaces;

        public static Expression CopyAndModify(Expression expression, IQueryable places)
        {
            var visitor = new DataQueryableExpressionTreeModifier(places);
            return visitor.Visit(expression);
        }

        private DataQueryableExpressionTreeModifier(IQueryable places)
        {
            _queryablePlaces = places;
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (typeof(IDataQueryable).IsAssignableFrom(node.Type))
                return Expression.Constant(_queryablePlaces);

            return base.VisitConstant(node);
        }
    }
}
