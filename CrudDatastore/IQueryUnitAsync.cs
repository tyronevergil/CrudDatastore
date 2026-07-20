using System;

namespace CrudDatastore
{
    /// <summary>
    /// Asynchronous-only query unit contract.
    /// Implement this interface when you provide only asynchronous query operations.
    /// </summary>
    public interface IQueryUnitAsync : IQueryUnitDefinition, ICommandAsync, IDisposable
    {
    }
}
