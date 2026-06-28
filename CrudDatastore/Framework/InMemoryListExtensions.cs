using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace CrudDatastore.Framework
{
    public static class InMemoryListExtensions
    {
        public static IDataQuery<T> CreateDataQuery<T>(this IList<T> instance) where T : EntityBase
        {
            return new DataQuery<T>(
                new DelegateQueryAdapter<T>(
                        /* read */
                        (predicate) =>
                        {
                            return instance.Where(predicate.Compile()).AsQueryable();
                        }
                    )
                );
        }

        public static IDataQuery<T> CreateDataQueryAsync<T>(this IList<T> instance) where T : EntityBase
        {
            return new DataQuery<T>(
                new DelegateQueryAdapter<T>(
                        /* read async */
                        async (predicate) =>
                        {
                            return await Task.Run(() => instance.Where(predicate.Compile()).AsQueryable());
                        }
                    )
                );
        }

        public static IDataStore<T> CreateDataStore<T>(this IList<T> instance) where T : EntityBase, new()
        {
            return CreateDataStore(instance, GetPropertyKey<T>());
        }

        public static IDataStore<T> CreateDataStore<T>(this IList<T> instance, Expression<Func<T, object>> key) where T : EntityBase, new()
        {
            return CreateDataStore(instance, key, IsIdentityKey<T>(GetPropertyKeyName(key)));
        }

        public static IDataStore<T> CreateDataStore<T>(this IList<T> instance, Expression<Func<T, object>> key, bool isIdentity) where T : EntityBase, new()
        {
            return CreateDataStore(instance, GetPropertyKeyName(key), isIdentity);
        }

        public static IDataStore<T> CreateDataStore<T>(this IList<T> instance, string keyName, bool isIdentity) where T : EntityBase, new()
        {
            var fieldList = typeof(T).GetProperties().Where(p => p.PropertyType.IsSealed && p.GetAccessors().Any(a => !(a.IsVirtual && !a.IsFinal) && a.ReturnType == typeof(void))).Select(p => p.Name).ToList();
            var fieldListWithoutKey = fieldList.Where(f => !string.Equals(f, keyName, StringComparison.OrdinalIgnoreCase)).ToList();

            return new DataStore<T>(
                new DelegateCrudAdapter<T>(
                        /* create */
                        (e) =>
                        {
                            var t = typeof(T);

                            if (isIdentity)
                            {
                                var param = Expression.Parameter(t, "e");
                                var prop = Expression.Property(param, keyName);

                                var selector = Expression.Lambda(prop, param);

                                var nextId = (instance.Any() ? instance.Max((Func<T, int>)selector.Compile()) : 0) + 1;
                                t.GetProperty(keyName).SetValue(e, nextId);
                            }

                            var entry = new T();
                            foreach (var field in fieldList)
                            {
                                var f = t.GetProperty(field);
                                f.SetValue(entry, f.GetValue(e));
                            }

                            instance.Add(entry);
                        },

                        /* update */
                        (e) =>
                        {
                            var entry = instance.FirstOrDefault(CreateKeyPredicate(e, keyName));
                            if (entry != null)
                            {
                                var t = typeof(T);
                                foreach (var field in fieldListWithoutKey)
                                {
                                    var f = t.GetProperty(field);
                                    f.SetValue(entry, f.GetValue(e));
                                }
                            }
                        },

                        /* delete */
                        (e) =>
                        {
                            var entry = instance.FirstOrDefault(CreateKeyPredicate(e, keyName));
                            if (entry != null)
                            {
                                instance.Remove(entry);
                            }
                        },

                        /* read */
                        (predicate) =>
                        {
                            return instance.Where(predicate.Compile()).AsQueryable();
                        }
                    )
                );
        }

        public static IDataStore<T> CreateDataStoreAsync<T>(this IList<T> instance) where T : EntityBase, new()
        {
            return CreateDataStoreAsync(instance, GetPropertyKey<T>());
        }

        public static IDataStore<T> CreateDataStoreAsync<T>(this IList<T> instance, Expression<Func<T, object>> key) where T : EntityBase, new()
        {
            return CreateDataStoreAsync(instance, key, IsIdentityKey<T>(GetPropertyKeyName(key)));
        }

        public static IDataStore<T> CreateDataStoreAsync<T>(this IList<T> instance, Expression<Func<T, object>> key, bool isIdentity) where T : EntityBase, new()
        {
            return CreateDataStoreAsync(instance, GetPropertyKeyName(key), isIdentity);
        }

        public static IDataStore<T> CreateDataStoreAsync<T>(this IList<T> instance, string keyName, bool isIdentity) where T : EntityBase, new()
        {
            var fieldList = typeof(T).GetProperties().Where(p => p.PropertyType.IsSealed && p.GetAccessors().Any(a => !(a.IsVirtual && !a.IsFinal) && a.ReturnType == typeof(void))).Select(p => p.Name).ToList();
            var fieldListWithoutKey = fieldList.Where(f => !string.Equals(f, keyName, StringComparison.OrdinalIgnoreCase)).ToList();

            return new DataStore<T>(
                new DelegateCrudAdapter<T>(
                        /* create */
                        async (e) =>
                        {
                            var t = typeof(T);

                            if (isIdentity)
                            {
                                var param = Expression.Parameter(t, "e");
                                var prop = Expression.Property(param, keyName);

                                var selector = Expression.Lambda(prop, param);

                                var nextId = (instance.Any() ? instance.Max((Func<T, int>)selector.Compile()) : 0) + 1;
                                t.GetProperty(keyName).SetValue(e, nextId);
                            }

                            var entry = new T();
                            foreach (var field in fieldList)
                            {
                                var f = t.GetProperty(field);
                                f.SetValue(entry, f.GetValue(e));
                            }

                            instance.Add(entry);

                            await Task.CompletedTask;
                        },

                        /* update */
                        async (e) =>
                        {
                            var entry = instance.FirstOrDefault(CreateKeyPredicate(e, keyName));
                            if (entry != null)
                            {
                                var t = typeof(T);
                                foreach (var field in fieldListWithoutKey)
                                {
                                    var f = t.GetProperty(field);
                                    f.SetValue(entry, f.GetValue(e));
                                }
                            }

                            await Task.CompletedTask;
                        },

                        /* delete */
                        async (e) =>
                        {
                            var entry = instance.FirstOrDefault(CreateKeyPredicate(e, keyName));
                            if (entry != null)
                            {
                                instance.Remove(entry);
                            }

                            await Task.CompletedTask;
                        },

                        /* read */
                        async (predicate) =>
                        {
                            return await Task.Run(() => instance.Where(predicate.Compile()).AsQueryable());
                        }
                    )
                );
        }
        
        private static Expression<Func<T, object>> GetPropertyKey<T>() where T : EntityBase
        {
            var type = typeof(T);
            var possibleKeys = new[] { "Id", string.Format("{0}Id", type.Name) };
            var keys = type.GetProperties()
                .Where(p => possibleKeys.Contains(p.Name, StringComparer.OrdinalIgnoreCase))
                .Select(p => p.Name)
                .ToList();

            if (keys.Any())
            {
                var param = Expression.Parameter(typeof(T));
                var field = Expression.Convert(Expression.PropertyOrField(param, keys.First()), typeof(object));
                return Expression.Lambda<Func<T, object>>(field, param);
            }
            else
            {
                throw new ArgumentException("No key property.");
            }
        }

        private static string GetPropertyKeyName<T>(Expression<Func<T, object>> key) where T : EntityBase
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

        private static bool IsIdentityKey<T>(string keyName) where T : EntityBase
        {
            var prop = typeof(T).GetProperty(keyName);
            if (prop != null)
            {
                return typeof(int).IsAssignableFrom(prop.PropertyType) || typeof(long).IsAssignableFrom(prop.PropertyType);
            }

            return false;
        }

        private static Func<T, bool> CreateKeyPredicate<T>(T entry, string keyName) where T : EntityBase
        {
            var t = typeof(T);

            var param = Expression.Parameter(t, "e");
            var prop = Expression.Property(param, keyName);
            var value = Expression.Constant(t.GetProperty(keyName).GetValue(entry));

            var predicate = Expression.Lambda(Expression.Equal(prop, value), param);

            return (Func<T, bool>)predicate.Compile();
        }
    }
}
