using System;

namespace CrudDatastore
{
    /// <summary>
    /// Full-featured query unit contract supporting both synchronous and asynchronous operations.
    /// Implement this interface when you provide both sync and async query operations.
    /// 
    /// For synchronous-only implementations, implement <see cref="IQueryUnitSync"/> instead.
    /// For asynchronous-only implementations, implement <see cref="IQueryUnitAsync"/> instead.
    /// </summary>
    public interface IQueryUnit : IQueryUnitSync, IQueryUnitAsync
    {
    }
}
