using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using CrudDatastore;

namespace CrudDatastore.Test
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

            var dataStorePerson = new DataStore<Entities.Person>(
                new DelegateCrudAdapter<Entities.Person>(this,
                    /* createTrigger */
                    (e) =>
                    {
                        var nextId = (people.Any() ? people.Max(p => p.PersonId) : 0) + 1;
                        e.PersonId = nextId;

                        people.Add(new Entities.Person
                        {
                            PersonId = e.PersonId,
                            Firstname = e.Firstname,
                            Lastname = e.Lastname
                        });
                    },

                    /* updateTrigger */
                    (e) =>
                    {
                        var person = people.FirstOrDefault(p => p.PersonId == e.PersonId);
                        if (person != null)
                        {
                            person.Firstname = e.Firstname;
                            person.Lastname = e.Lastname;
                        }
                    },

                    /* deleteTrigger */
                    (e) =>
                    {
                        var person = people.FirstOrDefault(p => p.PersonId == e.PersonId);
                        if (person != null)
                        {
                            people.Remove(person);
                        }
                    },

                    /* readExpressionTrigger */
                    (predicate) =>
                    {
                        return people.Where(predicate.Compile()).AsQueryable();
                    }
                )
            );

            var dataStoreIdentification = new DataStore<Entities.Identification>(
                new DelegateCrudAdapter<Entities.Identification>(
                    /* createTrigger */
                    (e) =>
                    {
                        var nextId = (identifications.Any() ? identifications.Max(i => i.IdentificationId) : 0) + 1;
                        e.IdentificationId = nextId;

                        identifications.Add(new Entities.Identification
                        {
                            IdentificationId = e.IdentificationId,
                            PersonId = e.PersonId,
                            Type = e.Type,
                            Number = e.Number
                        });
                    },

                    /* updateTrigger */
                    (e) =>
                    {
                        var identification = identifications.FirstOrDefault(i => i.IdentificationId == e.IdentificationId);
                        if (identification != null)
                        {
                            identification.PersonId = e.PersonId;
                            identification.Type = e.Type;
                            identification.Number = e.Number;
                        }
                    },

                    /* deleteTrigger */
                    (e) =>
                    {
                        var identification = identifications.FirstOrDefault(i => i.IdentificationId == e.IdentificationId);
                        if (identification != null)
                        {
                            identifications.Remove(identification);
                        }
                    },

                    /* readExpressionTrigger */
                    (predicate) =>
                    {
                        return identifications.Where(predicate.Compile()).AsQueryable();
                    }
                )
            );

            this.Register(dataStorePerson)
                .Map(p => p.Identifications, (p, i) => p.PersonId == i.PersonId);
            this.Register(dataStoreIdentification);
        }
    }
}
