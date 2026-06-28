using System;
using System.Threading.Tasks;

namespace CrudDatastore
{
    public interface ICommandContext
    {
        void Execute(IAction action);
        Task ExecuteAsync(IAction action);
    }
}
