using System;

namespace CrudDatastore
{
    /// <summary>
    /// Unit of work events contract.
    /// Provides event notifications for entity lifecycle changes (Create, Update, Delete).
    /// </summary>
    public interface IUnitOfWorkEvents
    {
        event EventHandler<EntityEventArgs> EntityCreate;
        event EventHandler<EntityEventArgs> EntityUpdate;
        event EventHandler<EntityEventArgs> EntityDelete;
    }
}
