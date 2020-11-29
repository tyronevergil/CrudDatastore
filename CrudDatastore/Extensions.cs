using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace CrudDatastore
{
    /* http://stackoverflow.com/questions/4035719/getmethod-for-generic-method */
    internal static class TypeExtensions
    {
        public static MethodInfo GetGenericMethod(this Type type, string name, Type[] parameterTypes)
        {
            var methods = type.GetMethods();
            foreach (var method in methods.Where(m => m.Name == name && m.IsGenericMethod))
            {
                var methodParameterTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();

                if (methodParameterTypes.SequenceEqual(parameterTypes, new SimpleTypeComparer()))
                {
                    return method;
                }
            }

            return null;
        }

        public static MethodInfo GetGenericMethod(this Type type, string name, Type[] parameterTypes, Type returnType)
        {
            var methods = type.GetMethods();
            foreach (var method in methods.Where(m => m.Name == name && m.IsGenericMethod))
            {
                var methodParameterTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();

                if (methodParameterTypes.SequenceEqual(parameterTypes, new SimpleTypeComparer()))
                {
                    if (new SimpleTypeComparer().Equals(method.ReturnType, returnType))
                        return method;
                }
            }

            return null;
        }

        private class SimpleTypeComparer : IEqualityComparer<Type>
        {
            public bool Equals(Type x, Type y)
            {
                return x.Assembly == y.Assembly &&
                    x.Namespace == y.Namespace &&
                    x.Name == y.Name;
            }

            public int GetHashCode(Type obj)
            {
                throw new NotImplementedException();
            }
        }
    }

    public static class ExpressionExtensions
    {
        public static Expression<Func<T, bool>> AndAlso<T>(this Expression<Func<T, bool>> left, Expression<Func<T, bool>> right) where T : EntityBase
        {
            var typeParam = left.Parameters[0];
            var expression = new ParameterVisitor(typeParam).Visit(right.Body);

            var combinedPredicates = Expression.AndAlso(left.Body, expression);
            return Expression.Lambda<Func<T, bool>>(combinedPredicates, typeParam);
        }

        public static Expression<Func<T, bool>> OrElse<T>(this Expression<Func<T, bool>> left, Expression<Func<T, bool>> right) where T : EntityBase
        {
            var typeParam = left.Parameters[0];
            var expression = new ParameterVisitor(typeParam).Visit(right.Body);

            var combinedPredicates = Expression.OrElse(left.Body, expression);
            return Expression.Lambda<Func<T, bool>>(combinedPredicates, typeParam);
        }

        public static Specification<T> AndAlso<T>(this Specification<T> spefication, Expression<Func<T, bool>> predicate) where T : EntityBase
        {
            return new Specification<T>(((Expression<Func<T, bool>>)spefication).AndAlso(predicate));
        }

        public static Specification<T> OrElse<T>(this Specification<T> spefication, Expression<Func<T, bool>> predicate) where T : EntityBase
        {
            return new Specification<T>(((Expression<Func<T, bool>>)spefication).OrElse(predicate));
        }

        public static Specification<T> AndAlso<T>(this Specification<T> left, Specification<T> right) where T : EntityBase
        {
            return new Specification<T>(((Expression<Func<T, bool>>)left).AndAlso(right));
        }

        public static Specification<T> OrElse<T>(this Specification<T> left, Specification<T> right) where T : EntityBase
        {
            return new Specification<T>(((Expression<Func<T, bool>>)left).OrElse(right));
        }

        internal class ParameterVisitor : ExpressionVisitor
        {
            private readonly ParameterExpression _parameterExpression;

            public ParameterVisitor(ParameterExpression parameterExpression)
            {
                _parameterExpression = parameterExpression;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                if (node.Type.IsAssignableFrom(_parameterExpression.Type))
                    return _parameterExpression;

                return base.VisitParameter(node);
            }
        }
    }
}
