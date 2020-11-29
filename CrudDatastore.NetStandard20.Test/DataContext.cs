using System;
using System.IO;
using CrudDatastore;

namespace CrudDatastore.Test
{
    public class DataContext : DataContextBase
    {
        private DataContext(IUnitOfWork unitOfWork)
            : base(unitOfWork)
        {
        }

        public static DataContext Factory()
        {
            return new DataContext(new UnitOfWorkInMemory());
        }
    }
}
