using System;
using System.Linq;
using System.Linq.Expressions;

namespace CrudDatastore
{
	public class DelegateQueryAdapter<T> : IQuery<T> where T : EntityBase
	{
        private readonly IDataNavigation _dataNavigation;
		private readonly Func<Expression<Func<T, bool>>, IQueryable<T>> _readExpressionTrigger;
		private readonly Func<string, object[], IQueryable<T>> _readCommandTrigger;

        public DelegateQueryAdapter(Func<Expression<Func<T, bool>>, IQueryable<T>> readExpressionTrigger)
            : this(readExpressionTrigger, (sql, parameters) => readExpressionTrigger((e) => false))
        {
        }

        public DelegateQueryAdapter(IDataNavigation dataNavigation, Func<Expression<Func<T, bool>>, IQueryable<T>> readExpressionTrigger)
            : this(dataNavigation, readExpressionTrigger, (sql, parameters) => readExpressionTrigger((e) => false))
        {
        }

        public DelegateQueryAdapter(Func<Expression<Func<T, bool>>, IQueryable<T>> readExpressionTrigger, Func<string, object[], IQueryable<T>> readCommandTrigger)
            : this(null, readExpressionTrigger, readCommandTrigger)
		{
		}

        public DelegateQueryAdapter(IDataNavigation dataNavigation, Func<Expression<Func<T, bool>>, IQueryable<T>> readExpressionTrigger, Func<string, object[], IQueryable<T>> readCommandTrigger)
        {
            _dataNavigation = dataNavigation;
            _readExpressionTrigger = readExpressionTrigger;
            _readCommandTrigger = readCommandTrigger;
        }

        public virtual IQueryable<T> Execute(Expression<Func<T, bool>> predicate)
		{
            if (_dataNavigation != null)
            {
                var modifiedPredicate = (Expression<Func<T, bool>>)InterceptNavigationPropertyExpressionTreeModifier.CopyAndModify(predicate, (entry, prop) => _dataNavigation.GetNavigationProperty(entry, prop));
                return _readExpressionTrigger(modifiedPredicate);
            }

			return _readExpressionTrigger(predicate);
		}

		public virtual IQueryable<T> Execute(string sql, params object[] parameters)
		{
			return _readCommandTrigger(sql, parameters);
		}
	}

    internal class InterceptNavigationPropertyExpressionTreeModifier : ExpressionVisitor
    {
        private readonly Func<object, string, object> _intercept;
        private ParameterExpression _param;

        public static Expression CopyAndModify(Expression expression, Func<object, string, object> intercept)
        {
            var lambda = expression as LambdaExpression;
            var param = default(ParameterExpression);

            if (lambda != null)
            {
                param = lambda.Parameters.First();
            }

            var visitor = new InterceptNavigationPropertyExpressionTreeModifier(param, intercept);
            return visitor.Visit(expression);
        }

        private InterceptNavigationPropertyExpressionTreeModifier(ParameterExpression param, Func<object, string, object> intercept)
        {
            _param = param;
            _intercept = intercept;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            var prop = node.Arguments.First() as MemberExpression;
            if (_param != null && prop != null && _param.Type == prop.Member.ReflectedType)
            {
                var interceptExpression = ModifyWithInterceptExpression(prop);
                var expressionCall = Expression.Call(node.Method, (new[] { interceptExpression }).Union(node.Arguments.Skip(1)).ToArray());
                return expressionCall;
            }

            return base.VisitMethodCall(node);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (_param != null && _param.Type == node.Expression.Type && !(node.Type.IsValueType || Type.GetTypeCode(node.Type) == TypeCode.String))
            {
                var interceptExpression = ModifyWithInterceptExpression(node);
                return interceptExpression;
            }

            return base.VisitMember(node);
        }

        private Expression ModifyWithInterceptExpression(MemberExpression prop)
        {
            var paramConverted = Expression.Convert(_param, typeof(object));
            var propName = Expression.Constant(prop.Member.Name);

            var interceptExpressionCall = _intercept.Target == null ?
                Expression.Call(_intercept.Method, paramConverted, propName) :
                Expression.Call(Expression.Constant(_intercept.Target), _intercept.Method, paramConverted, propName);
            var interceptExpressionConverted = Expression.Convert(interceptExpressionCall, prop.Type);

            return interceptExpressionConverted;
        }
    }
}
