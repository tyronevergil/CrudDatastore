namespace CrudDatastore
{
    /// <summary>
    /// Synchronous command execution contract.
    /// Implement this interface when you provide only synchronous Execute operations.
    /// </summary>
    public interface ICommandSync
    {
        void Execute(string command, params object[] parameters);
    }
}
