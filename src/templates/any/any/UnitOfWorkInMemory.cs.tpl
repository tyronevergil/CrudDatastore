using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using CrudDatastore;
using CrudDatastore.Framework;

namespace {{RootNamespace}}
{
    public class UnitOfWorkInMemory : UnitOfWorkBase
    {
        public UnitOfWorkInMemory()
        {
            var people = new List<Entities.Person>
            {
                new Entities.Person { PersonId = 1, Firstname = "Hermann", Lastname = "Einstein "},
                new Entities.Person { PersonId = 2, Firstname = "Albert", Lastname = "Einstein "},
                new Entities.Person { PersonId = 3, Firstname = "Maja", Lastname = "Einstein "}
            };

            var identifications = new List<Entities.Identification>
            {
                new Entities.Identification { IdentificationId = 1, PersonId = 1, Type = Entities.Identification.Types.SSN, Number = "509–515-224" },
                new Entities.Identification { IdentificationId = 2, PersonId = 1, Type = Entities.Identification.Types.TIN, Number = "92–4267" },
                new Entities.Identification { IdentificationId = 3, PersonId = 2, Type = Entities.Identification.Types.SSN, Number = "425–428-336" },
            };

            var dataStorePerson = people.CreateDataStore(p => p.PersonId);
            var dataStoreIdentification = identifications.CreateDataStore(p => p.IdentificationId);

            this.Register(dataStorePerson)
                .Map(p => p.Identifications, (p, i) => p.PersonId == i.PersonId);
            this.Register(dataStoreIdentification);
        }
    }
}
