using System;
using System.Linq.Expressions;

namespace CrudDatastore.Framework.Internal
{
    internal class ParameterReplacerVisitor : ExpressionVisitor
    {
        private readonly Expression _expression;

        public ParameterReplacerVisitor(Expression expression)
        {
            _expression = expression;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (node.Type.IsAssignableFrom(_expression.Type))
                return _expression;

            return base.VisitParameter(node);
        }
    }
}
