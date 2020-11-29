using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace CrudDatastore
{
    public interface IDataProvider
    {
        event EventHandler<EntityEventArgs> EntityMaterialized;

        IDataQuery<T> Read<T>() where T : EntityBase;
    }

    public interface IUnitOfWork : IDataProvider, IDataCommand, IDisposable
    {
        event EventHandler<EntityEventArgs> EntityCreate;
        event EventHandler<EntityEventArgs> EntityUpdate;
        event EventHandler<EntityEventArgs> EntityDelete;

        void MarkNew<T>(T entity) where T : EntityBase;
        void MarkModified<T>(T entity) where T : EntityBase;
        void MarkDeleted<T>(T entity) where T : EntityBase;

        void Commit();
    }

    public interface IDataMapping
    {
        IPropertyMap<T> Register<T>(IDataStore<T> dataStore) where T : EntityBase;
        IPropertyMap<T1> Map<T1, T2>(Expression<Func<T1, T2>> prop, Expression<Func<T1, T2, bool>> predicate) where T1 : EntityBase where T2 : EntityBase;
        IPropertyMap<T1> Map<T1, T2>(Expression<Func<T1, ICollection<T2>>> prop, Expression<Func<T1, T2, bool>> predicate) where T1 : EntityBase where T2 : EntityBase;
        IPropertyMap<T1> Map<T1, T2, T3>(Expression<Func<T1, ICollection<T2>>> prop, Expression<Func<T3, Expression<Func<T1, T2, bool>>>> mapping) where T1 : EntityBase where T2 : EntityBase where T3 : EntityBase;

        bool IsRegistered<T>() where T : EntityBase;
    }

    public interface IDataNavigation : IDataMapping
    {
        object GetNavigationProperty(object entry, string prop);
        object ResolveNavigationProperties(object entry);
        void ResolveAddedNavigationProperties(object entry);
    }
}
