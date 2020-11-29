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
            var currentDataDirectory = AppDomain.CurrentDomain.GetData("DataDirectory") as string;
            if (string.IsNullOrEmpty(currentDataDirectory))
            {
                var currentDirectory = Directory.GetCurrentDirectory();
                var d = currentDirectory.IndexOf("/bin");
                if (d < 0)
                    d = currentDirectory.IndexOf(@"\bin");

                if (d > 0)
                {
                    currentDirectory = currentDirectory.Substring(0, d);
                }
                AppDomain.CurrentDomain.SetData("DataDirectory", currentDirectory + "/App_Data");
            }

            return new DataContext(new UnitOfWorkInMemory());
        }
    }
}
