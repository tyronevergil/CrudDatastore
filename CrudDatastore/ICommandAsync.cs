using System.Threading.Tasks;

namespace CrudDatastore
{
    /// <summary>
    /// Asynchronous command execution contract.
    /// Implement this interface when you provide only asynchronous ExecuteAsync operations.
    /// </summary>
    public interface ICommandAsync
    {
        Task ExecuteAsync(string command, params object[] parameters);
    }
}
