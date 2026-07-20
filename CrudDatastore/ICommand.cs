namespace CrudDatastore
{
    /// <summary>
    /// Full-featured command execution contract supporting both synchronous and asynchronous operations.
    /// Implement this interface when you provide both Execute and ExecuteAsync operations.
    /// 
    /// For synchronous-only implementations, implement <see cref="ICommandSync"/> instead.
    /// For asynchronous-only implementations, implement <see cref="ICommandAsync"/> instead.
    /// </summary>
    public interface ICommand : ICommandSync, ICommandAsync
    {
    }
}
