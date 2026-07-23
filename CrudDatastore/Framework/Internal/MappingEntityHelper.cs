using System;
using System.Linq.Expressions;

namespace CrudDatastore.Framework.Internal
{
    internal static class MappingEntityHelper
    {
        public static object CreateMappingEntity(Type mappingEntityType, object parent, Type parentType, object entity, Type entityType)
        {
            var mappingEntity = Activator.CreateInstance(mappingEntityType);
            foreach (var p in mappingEntityType.GetProperties())
            {
                var value = default(object);

                var parentProp = parentType.GetProperty(p.Name);
                if (parentProp != null)
                {
                    value = parentProp.GetValue(parent);
                }

                var entityProp = entityType.GetProperty(p.Name);
                if (entityProp != null)
                {
                    value = entityProp.GetValue(entity);
                }

                p.SetValue(mappingEntity, value, null);
            }

            return mappingEntity;
        }

        public static Expression BuildEntityMatchExpression(Type mappingEntityType, Type entityType, object entity, ParameterExpression parameter)
        {
            var expression = default(Expression);
            foreach (var p in mappingEntityType.GetProperties())
            {
                var entityProp = entityType.GetProperty(p.Name);
                if (entityProp != null)
                {
                    var propExpression = Expression.Equal(
                        Expression.Property(parameter, entityProp.Name),
                        Expression.Constant(entityProp.GetValue(entity)));
                    expression = expression != null ? Expression.AndAlso(expression, propExpression) : propExpression;
                }
            }

            return expression;
        }
    }
}