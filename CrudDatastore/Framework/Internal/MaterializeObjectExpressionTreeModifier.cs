using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace CrudDatastore.Framework.Internal
{
    internal class MaterializeObjectExpressionTreeModifier : ExpressionVisitor
    {
        private readonly Func<object, object> _materializeObject;

        public static Expression CopyAndModify(Expression expression, Func<object, object> materializeObject)
        {
            var visitor = new MaterializeObjectExpressionTreeModifier(materializeObject);
            return visitor.Visit(expression);
        }

        private MaterializeObjectExpressionTreeModifier(Func<object, object> materializeObject)
        {
            _materializeObject = materializeObject;
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            var type = node.Type;
            if (type.IsGenericType)
            {
                var entityType = type.GetGenericArguments().First();
                if (typeof(EntityBase).IsAssignableFrom(entityType))
                {
                    if (typeof(IEnumerable<>).IsAssignableFrom(type.GetGenericTypeDefinition()) || typeof(EnumerableQuery<>).IsAssignableFrom(type.GetGenericTypeDefinition()))
                    {
                        var asqueryableMethod = typeof(Queryable).GetGenericMethod(nameof(Queryable.AsQueryable), new Type[] { typeof(IEnumerable<>) });
                        asqueryableMethod = asqueryableMethod.MakeGenericMethod(entityType);

                        var newMethodCall = Expression.Call(asqueryableMethod, node);
                        return VisitMethodCall(newMethodCall);
                    }
                }
            }

            return base.VisitConstant(node);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            var type = node.Method.ReturnType;
            if (type.IsGenericType && typeof(IQueryable<>).IsAssignableFrom(type.GetGenericTypeDefinition()))
            {
                var entityType = type.GetGenericArguments().First();

                if (typeof(EntityBase).IsAssignableFrom(entityType))
                {
                    var typeParam = Expression.Parameter(entityType);
                    var typeParamConverted = Expression.Convert(typeParam, typeof(object));
                    var materializeObjectExpressionCall = _materializeObject.Target == null ?
                        Expression.Call(_materializeObject.Method, typeParamConverted) :
                        Expression.Call(Expression.Constant(_materializeObject.Target), _materializeObject.Method, typeParamConverted);
                    var materializeObjectExpressionConverted = Expression.Convert(materializeObjectExpressionCall, entityType);

                    var selectMethod = typeof(Queryable).GetGenericMethod(nameof(Queryable.Select), new[] { typeof(IQueryable<>), typeof(Expression<>) });
                    selectMethod = selectMethod.MakeGenericMethod(new[] { entityType, entityType });

                    var lambdaMethod = typeof(Expression).GetGenericMethod(nameof(Expression.Lambda), new[] { typeof(Expression), typeof(ParameterExpression[]) })
                        .MakeGenericMethod(new[] { typeof(Func<,>)
                        .MakeGenericType(new[] { entityType, entityType }) });
                    var materializeObjectLambdaExpression = (LambdaExpression)lambdaMethod.Invoke(null, new object[] { materializeObjectExpressionConverted, new[] { typeParam } });

                    var selectExpressionCall = Expression.Call(selectMethod, node.Arguments.First(), materializeObjectLambdaExpression);
                    selectExpressionCall = Expression.Call(node.Method, (new[] { selectExpressionCall }).Union(node.Arguments.Skip(1)).ToArray());
#if (DEBUG)
                    System.Diagnostics.Debug.Print("MaterializeObjectExpressionTreeModifier : {0}", selectExpressionCall.ToString());
#endif
                    return selectExpressionCall;
                }
            }

            return base.VisitMethodCall(node);
        }
    }
}
