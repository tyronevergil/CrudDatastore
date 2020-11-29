using System;

namespace CrudDatastore
{
    public class Command : ICommand
    {
		private readonly string _command;
		private readonly object[] _parameters;

		public Command(string command, object[] parameters)
		{
			_command = command;
			_parameters = parameters;
		}

		public void SatisfyingFrom(IDataCommand dataCommand)
		{
			dataCommand.Execute(_command, _parameters);
		}
	}
}
