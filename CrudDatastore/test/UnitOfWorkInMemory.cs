using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using CrudDatastore;

namespace CrudDatastore.Test
{
    public class UnitOfWorkInMemory : UnitOfWorkBase
    {
        public UnitOfWorkInMemory()
        {
            var people = new List<Entities.Person>
            {
                new Entities.Person { PersonId = 1, Firstname = "Hermann", Lastname = "Einstein "},
                new Entities.Person { PersonId = 2, Firstname = "Albert", Lastname = "Einstein "},
                new Entities.Person { PersonId = 3, Firstname = "Maja", Lastname = "Einstein "}
            };

            var identifications = new List<Entities.Identification>
            {
                new Entities.Identification { IdentificationId = 1, PersonId = 1, Type = Entities.Identification.Types.SSN, Number = "509–515-224" },
                new Entities.Identification { IdentificationId = 2, PersonId = 1, Type = Entities.Identification.Types.TIN, Number = "92–4267" },
                new Entities.Identification { IdentificationId = 3, PersonId = 2, Type = Entities.Identification.Types.SSN, Number = "425–428-336" },
            };

            var dataStorePerson = new DataStore<Entities.Person>(
                new DelegateCrudAdapter<Entities.Person>(
                    /* createTrigger */
                    (e) =>
                    {
                        var nextId = (people.Any() ? people.Max(p => p.PersonId) : 0) + 1;
                        e.PersonId = nextId;

                        people.Add(new Entities.Person
                        {
                            PersonId = e.PersonId,
                            Firstname = e.Firstname,
                            Lastname = e.Lastname
                        });
                    },

                    /* updateTrigger */
                    (e) =>
                    {
                        var person = people.FirstOrDefault(p => p.PersonId == e.PersonId);
                        if (person != null)
                        {
                            person.Firstname = e.Firstname;
                            person.Lastname = e.Lastname;
                        }
                    },

                    /* deleteTrigger */
                    (e) =>
                    {
                        var person = people.FirstOrDefault(p => p.PersonId == e.PersonId);
                        if (person != null)
                        {
                            people.Remove(person);
                        }
                    },

                    /* readExpressionTrigger */
                    (predicate) =>
                    {
                        var modifiedPredicate = (Expression<Func<Entities.Person, bool>>)
                            new InterceptNavigationPropertyExpressionTreeModifier((entry, prop) =>
                            {
                                return ((IDataNavigation)this).GetNavigationProperty(entry, prop);
                            })
                            .CopyAndModify(predicate);
                        return people.Where(modifiedPredicate.Compile()).AsQueryable();
                    }
                )
            );

            var dataStoreIdentification = new DataStore<Entities.Identification>(
                new DelegateCrudAdapter<Entities.Identification>(
                    /* createTrigger */
                    (e) =>
                    {
                        var nextId = (identifications.Any() ? identifications.Max(i => i.IdentificationId) : 0) + 1;
                        e.IdentificationId = nextId;

                        identifications.Add(new Entities.Identification
                        {
                            IdentificationId = e.IdentificationId,
                            PersonId = e.PersonId,
                            Type = e.Type,
                            Number = e.Number
                        });
                    },

                    /* updateTrigger */
                    (e) =>
                    {
                        var identification = identifications.FirstOrDefault(i => i.IdentificationId == e.IdentificationId);
                        if (identification != null)
                        {
                            identification.PersonId = e.PersonId;
                            identification.Type = e.Type;
                            identification.Number = e.Number;
                        }
                    },

                    /* deleteTrigger */
                    (e) =>
                    {
                        var identification = identifications.FirstOrDefault(i => i.IdentificationId == e.IdentificationId);
                        if (identification != null)
                        {
                            identifications.Remove(identification);
                        }
                    },

                    /* readExpressionTrigger */
                    (predicate) =>
                    {
                        return identifications.Where(predicate.Compile()).AsQueryable();
                    }
                )
            );

            this.Register(dataStorePerson)
                .Map(p => p.Identifications, (p, i) => p.PersonId == i.PersonId);
            this.Register(dataStoreIdentification);
        }
    }

    internal class InterceptNavigationPropertyExpressionTreeModifier : ExpressionVisitor
    {
        private readonly Func<object, string, object> _intercept;
        private ParameterExpression _param;

        public InterceptNavigationPropertyExpressionTreeModifier(Func<object, string, object> intercept)
        {
            _intercept = intercept;
        }

        public Expression CopyAndModify(Expression expression)
        {
            var lambda = expression as LambdaExpression;
            if (lambda != null)
            {
                _param = lambda.Parameters.First();
            }

            return this.Visit(expression);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            var prop = node.Arguments.First() as MemberExpression;
            if (_param != null && _param.Type == prop.Member.ReflectedType)
            {
                var paramConverted = Expression.Convert(_param, typeof(object));
                var propName = Expression.Constant(prop.Member.Name);

                var interceptExpressionCall = _intercept.Target == null ?
                    Expression.Call(_intercept.Method, paramConverted, propName) :
                    Expression.Call(Expression.Constant(_intercept.Target), _intercept.Method, paramConverted, propName);
                var interceptExpressionConverted = Expression.Convert(interceptExpressionCall, prop.Type);

                var expressionCall = Expression.Call(node.Method, (new[] { interceptExpressionConverted }).Union(node.Arguments.Skip(1)).ToArray());

                return expressionCall;
            }

            return base.VisitMethodCall(node);
        }
    }
}
