using System;
using System.Linq;
using System.Linq.Expressions;
using CrudDatastore;

namespace {{RootNamespace}}
{
	public class DataContext : DataContextBase
    {
        private DataContext(IUnitOfWorkSync unitOfWorkSync)
            : base(unitOfWorkSync)
        {
        }

        private DataContext(IUnitOfWorkAsync unitOfWorkAsync)
            : base(unitOfWorkAsync)
        {
        }

        private DataContext(IUnitOfWork unitOfWork)
            : base(unitOfWork)
        {
        }

        public static DataContext Factory()
        {
            return new DataContext(new UnitOfWorkInMemory());
        }

        public static DataContext Factory(string connectionString)
        {
            return new DataContext(new UnitOfWorkEf(connectionString));
        }
    }
}
