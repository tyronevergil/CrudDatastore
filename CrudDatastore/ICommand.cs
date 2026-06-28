using System;
using System.Threading.Tasks;

namespace CrudDatastore
{
    public interface ICommand
    {
        void Execute(string command, params object[] parameters);
        Task ExecuteAsync(string command, params object[] parameters);
    }
}
