using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace CrudDatastore.Framework.Internal
{
    internal class PropertyMap<T1> : IPropertyMap<T1> where T1 : EntityBase
    {
        private readonly IDictionary<PropertyInfo, Delegate> _dataMapping;
        private readonly IDictionary<Tuple<Type, Type>, Type> _dataTableMapping;

        public PropertyMap(IDictionary<PropertyInfo, Delegate> dataMapping, IDictionary<Tuple<Type, Type>, Type> dataTableMapping)
        {
            _dataMapping = dataMapping;
            _dataTableMapping = dataTableMapping;
        }

        public IPropertyMap<T1> Map<T2>(Expression<Func<T1, T2>> prop, Expression<Func<T1, T2, bool>> predicate) where T2 : EntityBase
        {
            PropertyInfo propInfo = (PropertyInfo)((MemberExpression)prop.Body).Member;

            Func<T1, IDictionary<Type, IDataQuery>, Expression<Func<T2, bool>>> predicateBuilder = (t1, dataQueries) =>
            {
                var param = predicate.Parameters.FirstOrDefault(p => p.Type == typeof(T2));
                var body = (new ParameterReplacerVisitor(Expression.Constant(t1))).Visit(predicate.Body);

                return Expression.Lambda<Func<T2, bool>>(body, new[] { param });
            };

            return Map(propInfo, predicateBuilder);
        }

        public IPropertyMap<T1> Map<T2>(Expression<Func<T1, ICollection<T2>>> prop, Expression<Func<T1, T2, bool>> predicate) where T2 : EntityBase
        {
            PropertyInfo propInfo = (PropertyInfo)((MemberExpression)prop.Body).Member;

            Func<T1, IDictionary<Type, IDataQuery>, Expression<Func<T2, bool>>> predicateBuilder = (t1, dataQueries) =>
            {
                var param = predicate.Parameters.FirstOrDefault(p => p.Type == typeof(T2));
                var body = (new ParameterReplacerVisitor(Expression.Constant(t1))).Visit(predicate.Body);

                return Expression.Lambda<Func<T2, bool>>(body, new[] { param });
            };

            return Map(propInfo, predicateBuilder);
        }

        public IPropertyMap<T1> Map<T2, T3>(Expression<Func<T1, ICollection<T2>>> prop, Expression<Func<T3, Expression<Func<T1, T2, bool>>>> mapping) where T2 : EntityBase where T3 : EntityBase
        {
            PropertyInfo propInfo = (PropertyInfo)((MemberExpression)prop.Body).Member;

            Func<T1, IDictionary<Type, IDataQuery>, Expression<Func<T2, bool>>> predicateBuilder = (t1, dataQueries) =>
            {
                var table = (IDataQuery<T3>)dataQueries[typeof(T3)];
                // Always use async API for consistency; block only during initialization
                var tableData = table.FindAsync(new Specification<T3>(m => true)).GetAwaiter().GetResult().ToList().AsQueryable();

                var paramT3 = mapping.Parameters.FirstOrDefault(p => p.Type == typeof(T3));
                var bodyT3 = ((LambdaExpression)((UnaryExpression)mapping.Body).Operand).Body;

                var expressionT3 = Expression.Call(typeof(Queryable), "Any", new[] { typeof(T3) }, Expression.Constant(tableData),
                        Expression.Lambda<Func<T3, bool>>(bodyT3, new[] { paramT3 }));

                var expressionT1 = (new ParameterReplacerVisitor(Expression.Constant(t1))).Visit(expressionT3);

                var paramT2 = Expression.Parameter(typeof(T2));
                var expressionT2 = (new ParameterReplacerVisitor(paramT2)).Visit(expressionT1);


                var expression = Expression.Lambda<Func<T2, bool>>(expressionT2, new[] { paramT2 });
                return expression;
            };

            var keyTypes = Tuple.Create(typeof(T1), typeof(T2));
            if (!_dataTableMapping.ContainsKey(keyTypes))
            {
                _dataTableMapping.Add(keyTypes, typeof(T3));
            }

            return Map(propInfo, predicateBuilder);
        }

        private IPropertyMap<T1> Map<T2>(PropertyInfo prop, Func<T1, IDictionary<Type, IDataQuery>, Expression<Func<T2, bool>>> predicateBuilder)
        {
            if (!_dataMapping.ContainsKey(prop))
            {
                _dataMapping.Add(prop, predicateBuilder);
            }

            return new PropertyMap<T1>(_dataMapping, _dataTableMapping);
        }

    }
}
