using System;

namespace CrudDatastore
{
    public interface IQueryUnit : ICommand, IDisposable
    {
        event EventHandler<EntityEventArgs> EntityMaterialized;

        IDataQuery<T> Read<T>() where T : EntityBase;
    }
}
