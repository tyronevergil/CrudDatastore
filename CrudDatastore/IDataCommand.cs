using System;

namespace CrudDatastore
{
    public interface IDataCommand
    {
		void Execute(string command, params object[] parameters);
	}
}
