using System;
using System.Threading.Tasks;

namespace CrudDatastore
{
    /// <summary>
    /// Full-featured unit of work contract supporting both synchronous and asynchronous operations.
    /// Implement this interface when your unit of work provides both sync and async operations.
    /// 
    /// For synchronous-only implementations, implement <see cref="IUnitOfWorkSync"/> instead.
    /// For asynchronous-only implementations, implement <see cref="IUnitOfWorkAsync"/> instead.
    /// </summary>
    public interface IUnitOfWork : IUnitOfWorkSync, IUnitOfWorkAsync
    {
    }
}
