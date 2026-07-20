using System;

namespace CrudDatastore
{
    /// <summary>
    /// Query unit definition contract.
    /// Defines the core query capabilities and events for a query unit.
    /// </summary>
    public interface IQueryUnitDefinition
    {
        event EventHandler<EntityEventArgs> EntityMaterialized;

        IDataQuery<T> Read<T>() where T : EntityBase;
    }
}
