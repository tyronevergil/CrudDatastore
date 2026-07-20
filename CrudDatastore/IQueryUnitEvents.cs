using System;

namespace CrudDatastore
{
    /// <summary>
    /// Query unit events contract.
    /// Provides event notification when entities are materialized/loaded from the data store.
    /// </summary>
    public interface IQueryUnitEvents
    {
        event EventHandler<EntityEventArgs> EntityMaterialized;

        IDataQuery<T> Read<T>() where T : EntityBase;
    }
}
