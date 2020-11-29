using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace CrudDatastore
{
	public class DataQuery<T> : IDataQuery<T>, IQuery<T> where T : EntityBase
	{
		private readonly IQuery<T> _query;
        private readonly Func<T, T> _materializeObject;

		public DataQuery(IQuery<T> query)
            : this(query, null)
		{
		}

        internal DataQuery(IQuery<T> query, Func<T, T> materializeObject)
        {
            _query = query;
            _materializeObject = materializeObject;
        }

        public virtual IQueryable<T> Find(ISpecification<T> specification)
        {
            return specification.SatisfyingEntitiesFrom(this);
        }

        public virtual T FindSingle(ISpecification<T> specification)
        {
            return Find(specification).FirstOrDefault();
        }

        //protected virtual IQueryable<T> QueryableModifier(IQueryable<T> queryable)
        //{
        //    return queryable;
        //}

        IQueryable<T> IQuery<T>.Execute(Expression<Func<T, bool>> predicate)
        {
            return new DataQueryable<T>(() => _query.Execute(predicate), _materializeObject);
        }

        IQueryable<T> IQuery<T>.Execute(string command, params object[] parameters)
        {
            return new DataQueryable<T>(() => _query.Execute(command, parameters), _materializeObject);
        }
    }

    internal interface IDataQueryable
    {

    }

    internal class DataQueryable<T> : IOrderedQueryable<T>, IDataQueryable
    {
        private readonly Func<T, T> _materializeObject;

        public DataQueryable(Func<IQueryable<T>> dataSource, Func<T, T> materializeObject)
        {
            var materializeObjectUnboxed = default(Func<object, object>);
            if (materializeObject != null)
                materializeObjectUnboxed = (e) => materializeObject((T)e);

            Provider = new DataQueryableProvider(dataSource, materializeObjectUnboxed);
            Expression = Expression.Constant(this);

            _materializeObject = materializeObject;
        }

        public DataQueryable(IQueryProvider provider, Expression expression, Func<T, T> materializeObject)
        {
            Provider = provider;
            Expression = expression;

            _materializeObject = materializeObject;
        }

        public Expression Expression { get; private set; }

        public IQueryProvider Provider { get; private set; }

        public IEnumerator<T> GetEnumerator()
        {
            var enumerator = Provider.Execute<IEnumerable<T>>(Expression).GetEnumerator();
            if (_materializeObject != null)
                enumerator = new MaterializeObjectEnumerator<T>(enumerator, _materializeObject);
                
            return enumerator;
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

    internal class DataQueryableProvider : IQueryProvider
    {
        private readonly Func<IQueryable> _dataSource;
        private readonly Func<object, object> _materializeObject;

        public DataQueryableProvider(Func<IQueryable> dataSource, Func<object, object> materializeObject)
        {
            _dataSource = dataSource;
            _materializeObject = materializeObject;
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            var  materializeObject = default(Func<TElement, TElement>);
            if (_materializeObject != null)
                materializeObject = (e) => (TElement)_materializeObject(e);

            return new DataQueryable<TElement>(this, expression, materializeObject);
        }

        public IQueryable CreateQuery(System.Linq.Expressions.Expression expression)
        {
            throw new NotImplementedException();
        }

        public TResult Execute<TResult>(System.Linq.Expressions.Expression expression)
        {
            var queryableElements = _dataSource();
            var modifiedExpressionTree = new DataQueryableExpressionTreeModifier(queryableElements).CopyAndModify(expression);

            var isEnumerable = typeof(IEnumerable).IsAssignableFrom(typeof(TResult));
            if (isEnumerable)
            {
                if (_materializeObject != null)
                    modifiedExpressionTree = new MaterializeObjectExpressionTreeModifier(_materializeObject).CopyAndModify(modifiedExpressionTree);

                var elementType = typeof(TResult).GetGenericArguments().FirstOrDefault();
                var createQuery = typeof(IQueryProvider)
                    .GetGenericMethod("CreateQuery", new[] { typeof(Expression) }, typeof(IQueryable<>));
                var createQueryGeneric = createQuery
                    .MakeGenericMethod(new[] { elementType });
                return (TResult)createQueryGeneric.Invoke(queryableElements.Provider, new[] { modifiedExpressionTree });
                //return (TResult) queryableElements.Provider.CreateQuery(modifiedExpressionTree);
            }
            else
            {
                if (_materializeObject != null && typeof(EntityBase).IsAssignableFrom(expression.Type))
                    return (TResult)_materializeObject(queryableElements.Provider.Execute<TResult>(modifiedExpressionTree));
                else
                    return queryableElements.Provider.Execute<TResult>(modifiedExpressionTree);
            }
        }

        public object Execute(System.Linq.Expressions.Expression expression)
        {
            throw new NotImplementedException();
        }
    }

    internal class DataQueryableExpressionTreeModifier : ExpressionVisitor
    {
        private readonly IQueryable queryablePlaces;

        public DataQueryableExpressionTreeModifier(IQueryable places)
        {
            this.queryablePlaces = places;
        }

        public Expression CopyAndModify(Expression expression)
        {
            return this.Visit(expression);
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (typeof(IDataQueryable).IsAssignableFrom(node.Type))
                return Expression.Constant(this.queryablePlaces);

            return base.VisitConstant(node);
        }
    }

    internal class MaterializeObjectExpressionTreeModifier : ExpressionVisitor
    {
        private readonly Func<object, object> _materializeObject;

        public MaterializeObjectExpressionTreeModifier(Func<object, object> materializeObject)
        {
            _materializeObject = materializeObject;
        }

        public Expression CopyAndModify(Expression expression)
        {
            return this.Visit(expression);
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            var type = node.Type;
            if (type.IsGenericType)
            {
                var entityType = type.GetGenericArguments().First();
                if (typeof(EntityBase).IsAssignableFrom(entityType))
                {
                    var asqueryableMethod = typeof(Queryable).GetGenericMethod("AsQueryable", new Type[] { typeof(IEnumerable<>) });
                    asqueryableMethod = asqueryableMethod.MakeGenericMethod(entityType);

                    var newMethodCall = Expression.Call(asqueryableMethod, node);
                    return this.VisitMethodCall(newMethodCall);
                }
            }

            return base.VisitConstant(node);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            var type = node.Method.ReturnType;
            //var type = node.Type;
            if (type.IsGenericType && typeof(IQueryable<>).IsAssignableFrom(type.GetGenericTypeDefinition()))
            {
                var entityType = type.GetGenericArguments().First();

                //if (!typeof(EntityBase).IsAssignableFrom(entityType))
                //{
                //    var f = node.Arguments.First().Type;
                //    if (f.IsGenericType && typeof(EnumerableQuery<>).IsAssignableFrom(f.GetGenericTypeDefinition()))
                //    {
                //        entityType = f.GetGenericArguments().First();
                //    }
                //}

                if (typeof(EntityBase).IsAssignableFrom(entityType))
                {
                    var typeParam = Expression.Parameter(entityType);
                    var typeParamConverted = Expression.Convert(typeParam, typeof(object));
                    var materializeObjectExpressionCall = _materializeObject.Target == null ?
                        Expression.Call(_materializeObject.Method, typeParamConverted) :
                        Expression.Call(Expression.Constant(_materializeObject.Target), _materializeObject.Method, typeParamConverted);
                    var materializeObjectExpressionConverted = Expression.Convert(materializeObjectExpressionCall, entityType);

                    var selectMethod = typeof(Queryable).GetGenericMethod("Select", new[] { typeof(IQueryable<>), typeof(Expression<>) });
                    selectMethod = selectMethod.MakeGenericMethod(new[] { entityType, entityType });

                    var lambdaMethod = typeof(Expression).GetGenericMethod("Lambda", new[] { typeof(Expression), typeof(ParameterExpression[]) })
                        .MakeGenericMethod(new[] { typeof(Func<,>)
                        .MakeGenericType(new[] { entityType, entityType }) });
                    var materializeObjectLambdaExpression = (LambdaExpression)lambdaMethod.Invoke(null, new object[] { materializeObjectExpressionConverted, new[] { typeParam } });

                    var selectExpressionCall = Expression.Call(selectMethod, node.Arguments.First(), materializeObjectLambdaExpression);
                    selectExpressionCall = Expression.Call(node.Method, (new[] { selectExpressionCall }).Union(node.Arguments.Skip(1)).ToArray());
#if (DEBUG)
                    System.Diagnostics.Debug.Print("SelectMethodCallExpression : {0}", selectExpressionCall.ToString());
#endif
                    return selectExpressionCall;
                }
            }

            return base.VisitMethodCall(node);
        }
    }

    internal class MaterializeObjectEnumerator<T> : IEnumerator<T>
    {
        private readonly IEnumerator<T> _enumerator;
        private readonly Func<T, T> _materializeObject;

        public MaterializeObjectEnumerator(IEnumerator<T> enumerator, Func<T, T> materializeObject)
        {
            _enumerator = enumerator;
            _materializeObject = materializeObject;
        }

        public T Current
        {
            get
            {
                return typeof(EntityBase).IsAssignableFrom(typeof(T)) ? _materializeObject(_enumerator.Current) : _enumerator.Current;
            }
        }

        object IEnumerator.Current
        {
            get
            {
                return _enumerator.Current;
            }
        }

        public bool MoveNext()
        {
            return _enumerator.MoveNext();
        }

        public void Reset()
        {
            _enumerator.Reset();
        }

        public void Dispose()
        {
            _enumerator.Dispose();
        }
    }
}
