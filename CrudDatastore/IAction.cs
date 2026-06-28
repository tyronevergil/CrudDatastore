using System;
using System.Threading.Tasks;

namespace CrudDatastore
{
    public interface IAction
    {
        void SatisfyingActionFrom(ICommand command);
        Task SatisfyingActionFromAsync(ICommand command);
    }
}
