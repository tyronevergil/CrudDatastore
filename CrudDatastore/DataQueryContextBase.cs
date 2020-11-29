using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CrudDatastore
{
    public interface IDataQueryRegistry
    {
        void Register<T>(IDataQuery<T> dataQuery) where T : EntityBase;
        bool IsRegistered<T>() where T : EntityBase;
    }

    public abstract class DataQueryContextBase : IDataQueryRegistry, IDisposable
    {
        private bool _disposed;
        private bool _propDataQueriesAdded;

        private readonly IDictionary<Type, IDataQuery> _dataQueries = new Dictionary<Type, IDataQuery>();

        public virtual IQueryable<T> Find<T>(ISpecification<T> specification) where T : EntityBase
        {
            return GetDataQuery<T>().Find(specification);
        }

        public virtual T FindSingle<T>(ISpecification<T> specification) where T : EntityBase
        {
            return GetDataQuery<T>().FindSingle(specification);
        }

        public virtual void Execute(ICommand command)
        {
        }

        protected virtual void Register<T>(IDataQuery<T> dataQuery) where T : EntityBase
        {
            _dataQueries.Add(typeof(T), dataQuery);
        }

        protected virtual IDataQuery<T> GetDataQuery<T>() where T : EntityBase
        {
            if (!_propDataQueriesAdded)
            {
                _propDataQueriesAdded = true;
                foreach (var prop in this.GetType()
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p =>
                        (p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition().IsAssignableFrom(typeof(IDataQuery<>))) ||
                        (p.PropertyType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition().IsAssignableFrom(typeof(IDataQuery<>)))))
                .ToList())
                {
                    var t = prop.PropertyType.GetGenericArguments()[0];
                    if (!_dataQueries.ContainsKey(t))
                    {
                        var dataQuery = prop.GetValue(this);
                        _dataQueries.Add(t, (IDataQuery)dataQuery);
                    }
                }
            }

            var type = typeof(T);
            if (_dataQueries.ContainsKey(type))
                return (IDataQuery<T>)_dataQueries[type];

            return null;
        }

        void IDataQueryRegistry.Register<T>(IDataQuery<T> dataQuery)
        {
            Register<T>(dataQuery);
        }

        bool IDataQueryRegistry.IsRegistered<T>()
        {
            return _dataQueries.ContainsKey(typeof(T));
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Free other state (managed objects)
                }

                _dataQueries.Clear();

                //
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~DataQueryContextBase()
        {
            Dispose(false);
        }
    }
}
