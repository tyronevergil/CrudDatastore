using System;

namespace CrudDatastore
{
    /// <summary>
    /// Synchronous-only query unit contract.
    /// Implement this interface when you provide only synchronous query operations.
    /// </summary>
    public interface IQueryUnitSync : IQueryUnitDefinition, ICommandSync, IDisposable
    {
    }
}
