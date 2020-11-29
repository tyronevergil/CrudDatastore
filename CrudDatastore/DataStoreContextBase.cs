using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CrudDatastore
{
    public interface IDataStoreRegistry
    {
        void Register<T>(IDataStore<T> dataStore) where T : EntityBase;
        bool IsRegistered<T>() where T : EntityBase;
    }

    public abstract class DataStoreContextBase : DataQueryContextBase, IDataStoreRegistry
    {
        private bool _disposed;
        private bool _propDataStoreAdded;

        private readonly IDictionary<Type, IDataStore> _dataStores = new Dictionary<Type, IDataStore>();

        public virtual void Add<T>(T entity) where T : EntityBase
        {
            GetDataStore<T>().Add(entity);
        }

        public virtual void Update<T>(T entity) where T : EntityBase
        {
            GetDataStore<T>().Update(entity);
        }

        public virtual void Delete<T>(T entity) where T : EntityBase
        {
            GetDataStore<T>().Delete(entity);
        }

        protected virtual void Register<T>(IDataStore<T> dataStore) where T : EntityBase
        {
            base.Register(dataStore);

            _dataStores.Add(typeof(T), dataStore);            
        }

        protected virtual IDataStore<T> GetDataStore<T>() where T : EntityBase
        {
            if (!_propDataStoreAdded)
            {
                _propDataStoreAdded = true;
                foreach (var prop in this.GetType()
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p =>
                        (p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition().IsAssignableFrom(typeof(IDataStore<>))) ||
                        (p.PropertyType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition().IsAssignableFrom(typeof(IDataStore<>)))))
                    .ToList())
                {
                    var t = prop.PropertyType.GetGenericArguments()[0];
                    if (_dataStores.ContainsKey(t))
                    {
                        var dataStore = prop.GetValue(this);
                        _dataStores.Add(t, (IDataStore)dataStore);
                    }
                }
            }

            var type = typeof(T);
            if (_dataStores.ContainsKey(type))
                return (IDataStore<T>)_dataStores[type];

            return null;
        }

        void IDataStoreRegistry.Register<T>(IDataStore<T> dataStore)
        {
            Register<T>(dataStore);
        }

        bool IDataStoreRegistry.IsRegistered<T>()
        {
            return _dataStores.ContainsKey(typeof(T));
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Free other state (managed objects)
                }

                _dataStores.Clear();

                //
                _disposed = true;
            }

            base.Dispose(disposing);
        }

        ~DataStoreContextBase()
        {
            Dispose(false);
        }
    }
}
