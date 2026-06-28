using System;
using System.Threading.Tasks;

namespace CrudDatastore
{
    public class Action : IAction
    {
        private readonly string _command;
        private readonly object[] _parameters;

        public Action(string command, object[] parameters)
        {
            _command = command;
            _parameters = parameters;
        }

        public void SatisfyingActionFrom(ICommand command)
        {
            command.Execute(_command, _parameters);
        }

        public Task SatisfyingActionFromAsync(ICommand command)
        {
            return command.ExecuteAsync(_command, _parameters);
        }
    }
}
