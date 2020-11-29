using System;
using CrudDatastore;

namespace CrudDatastore.Test.Entities
{
    public class Identification : EntityBase
    {
        public int IdentificationId { get; set; }
        public int PersonId { get; set; }
        public Types Type { get; set; }
        public string Number { get; set; }

        public enum Types
        {
            SSN = 1,
            TIN
        }
    }
}
