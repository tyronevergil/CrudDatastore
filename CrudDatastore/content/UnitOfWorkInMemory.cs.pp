using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using CrudDatastore;

namespace $rootnamespace$
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

            var dataStorePerson = new DataStore<Entities.Person>(new InMemoryCrudAdapter<Entities.Person>(people, p => p.PersonId));
            var dataStoreIdentification = new DataStore<Entities.Identification>(new InMemoryCrudAdapter<Entities.Identification>(identifications, p => p.IdentificationId));                

            this.Register(dataStorePerson)
                .Map(p => p.Identifications, (p, i) => p.PersonId == i.PersonId);
            this.Register(dataStoreIdentification);
        }
    }

    internal class InMemoryCrudAdapter<T> : DelegateCrudAdapter<T> where T : EntityBase, new() 
    {
        private static IEnumerable<string> _fieldList;
        private static IEnumerable<string> _fieldListWithoutKey;

        public InMemoryCrudAdapter(IList<T> source, Expression<Func<T, object>> key)
            : this(source, key, true)
        { }

        public InMemoryCrudAdapter(IList<T> source, Expression<Func<T, object>> key, bool isIdentity)
            : this(source, GetPropertyName(key), true)
        { }

        private InMemoryCrudAdapter(IList<T> source, string key, bool isIdentity)
            : base
            (
                /* create */
                (e) =>
                {
                    var t = typeof(T);

                    if (isIdentity)
                    {
                        var param = Expression.Parameter(t, "e");
                        var prop = Expression.Property(param, key);

                        var selector = Expression.Lambda(prop, param);

                        var nextId = (source.Any() ? source.Max((Func<T, int>)selector.Compile()) : 0) + 1;
                        t.GetProperty(key).SetValue(e, nextId);
                    }

                    var entry = new T();
                    foreach (var field in _fieldList)
                    {
                        var f = t.GetProperty(field);
                        f.SetValue(entry, f.GetValue(e));
                    }

                    source.Add(entry);
                },

                /* update */
                (e) =>
                {
                    var entry = source.FirstOrDefault(CreatePredicate(e, key));
                    if (entry != null)
                    {
                        var t = typeof(T);
                        foreach (var field in _fieldListWithoutKey)
                        {
                            var f = t.GetProperty(field);
                            f.SetValue(entry, f.GetValue(e));
                        }
                    }
                },

                /* delete */
                (e) =>
                {
                    var entry = source.FirstOrDefault(CreatePredicate(e, key));
                    if (entry != null)
                    {
                        source.Remove(entry);
                    }
                },

                /* read */
                (predicate) =>
                {
                    return source.Where(predicate.Compile()).AsQueryable();
                },

                /* read */
                (sql, parameters) =>
                {
                    return Enumerable.Empty<T>().AsQueryable();
                }
            )
        {
            if (_fieldList == null)
            {
                _fieldList = typeof(T).GetProperties().Where(p => p.PropertyType.IsSealed && p.GetAccessors().Any(a => !(a.IsVirtual && !a.IsFinal) && a.ReturnType == typeof(void))).Select(p => p.Name).ToList();
                _fieldListWithoutKey = _fieldList.Where(f => !string.Equals(f, key, StringComparison.OrdinalIgnoreCase)).ToList();
            }
        }

        private static string GetPropertyName(Expression<Func<T, object>> key)
        {
            if (key.Body is UnaryExpression && ((UnaryExpression)key.Body).Operand is MemberExpression)
            {
                return ((MemberExpression)((UnaryExpression)key.Body).Operand).Member.Name;
            }
            else
            {
                throw new ArgumentException("Invalid key property.");
            }
        }

        private static Func<T, bool> CreatePredicate(T entry, string key)
        {
            var t = typeof(T);

            var param = Expression.Parameter(t, "e");
            var prop = Expression.Property(param, key);
            var value = Expression.Constant(t.GetProperty(key).GetValue(entry));

            var predicate = Expression.Lambda(Expression.Equal(prop, value), param);

            return (Func<T, bool>)predicate.Compile();
        }
    }
}
