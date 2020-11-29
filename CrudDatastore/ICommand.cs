using System;

namespace CrudDatastore
{
    public interface ICommand
    {
        void SatisfyingFrom(IDataCommand dataCommand);
    }
}
