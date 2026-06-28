using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace CrudDatastore.Framework.Internal
{
    internal class ConstantPropertiesVisitor<T> : ExpressionVisitor where T : EntityBase
    {
        private readonly Stack _pathToValue = new Stack();
        private readonly IDictionary<string, IDictionary<string, object>> _properties = new Dictionary<string, IDictionary<string, object>>();

        public static IDictionary<string, IDictionary<string, object>> GetProperties(Expression expression)
        {
            var visitor = new ConstantPropertiesVisitor<T>();
            visitor.Visit(expression);
            return visitor.GetProperties();
        }

        private ConstantPropertiesVisitor()
        {
        }

        private IDictionary<string, IDictionary<string, object>> GetProperties()
        {
            return _properties;
        }

        public override Expression Visit(Expression node)
        {
            _pathToValue.Push(node);
            var result = base.Visit(node);
            _pathToValue.Pop();

            return result;
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            var pathArray = _pathToValue.ToArray();
            var parentMemberExpression = pathArray.FirstOrDefault(e =>
            {
                var expression = ((Expression)e);
                if (expression.NodeType == ExpressionType.MemberAccess)
                {
                    var memberExpression = ((MemberExpression)expression);
                    if (memberExpression.Member is PropertyInfo && memberExpression.Expression is ConstantExpression)
                    {
                        return memberExpression.Expression.Type != typeof(T);
                    }
                }

                return false;
            });

            if (parentMemberExpression != null)
            {
                var memberExpression = (MemberExpression)parentMemberExpression;
                var propertyInfo = (PropertyInfo)memberExpression.Member;

                var prop = memberExpression.Member.Name;
                var value = propertyInfo.GetValue(node.Value, null); ;

                var comparisonExpression = pathArray.FirstOrDefault(e =>
                {
                    var expression = (Expression)e;
                    if (expression is BinaryExpression binary)
                    {
                        var memberLeft = binary.Left as MemberExpression;
                        if (memberLeft != null && memberLeft.Member is PropertyInfo)
                        {
                            if (memberLeft == parentMemberExpression)
                            {
                                return true;
                            }
                        }

                        var memberRight = binary.Right as MemberExpression;
                        if (memberRight != null && memberRight.Member is PropertyInfo)
                        {
                            if (memberRight == parentMemberExpression)
                            {
                                return true;
                            }
                        }
                    }

                    return false;
                });

                if (comparisonExpression != null)
                {
                    var binaryExpression = (BinaryExpression)comparisonExpression;
                    var comparisonMemberExpression = (binaryExpression.Left == parentMemberExpression ? binaryExpression.Right : binaryExpression.Left) as MemberExpression;

                    if (comparisonMemberExpression != null)
                    {
                        var propValues = new Dictionary<string, object>();
                        propValues.Add("value", value);
                        propValues.Add("comparison", comparisonMemberExpression.Member.Name);

                        _properties.Add(prop, propValues);
                    }
                }
            }

            return base.VisitConstant(node);
        }
    }
}
