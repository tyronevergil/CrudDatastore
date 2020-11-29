# CrudDatastore - Simple Data Access Framework

CrudDatastore is another Data Access Framework which utilizes Specification/Criterion pattern to query data using Linq Expression or query command and parameters to execute Database Stored Procedures. 

It provides DataQuery and DataStore classes as a simple way to access and modify data.

Or use DataContextBase and UnitOfWorkBase abstract classes as data storage container and UnitOfWork implementation with relational mapping support, can be as an InMemory data access strategy for Unit Testing or for simple persistent storage. 

## Installation

Use NuGet to install the [package](https://www.nuget.org/packages/CrudDatastore/).

```
PM> Install-Package CrudDatastore
```
CrudDatastore EntityFramework boilerplate.

```
PM> Install-Package CrudDatastore.EntityFramework
```

## DataQuery

DataQuery class access data using Specification/Criterion pattern using Linq Expression or providing query command and parameters.

**Usage**

```csharp

var dataQuery = new DataQuery<Person>(new DbContextQueryAdapter<Person>(/* your dbContext */));

// Find Person by specification
var person = dataQuery.FindSingle(PersonSpecs.GetPersonById(1));   

```
<details><summary>See more</summary>
<p>

**InMemory**

```csharp

var people = new List<Person>
    {
        new Person { PersonId = 1, Firstname = "Hermann", Lastname = "Einstein "},
        new Person { PersonId = 2, Firstname = "Albert", Lastname = "Einstein "},
        new Person { PersonId = 3, Firstname = "Maja", Lastname = "Einstein "}
    };

var dataQuery = new DataQuery<Person>(
    new DelegateQueryAdapter<Person>(
            /* read */
            (predicate) =>
            {
                return people.Where(predicate.Compile()).AsQueryable();
            }
        )
    );

// Find Person by specification
var person = dataQuery.FindSingle(PersonSpecs.GetPersonById(1)); 

```

<details><summary><b>rest of the codes</b></summary>
<p>

```csharp

public class Person : EntityBase
{
    public int PersonId { get; set; }
    public string Firstname { get; set; }
    public string Lastname { get; set; }
}
    
public class PersonSpecs : Specification<Person>
{
    private PersonSpecs(Expression<Func<Person, bool>> predicate)
        : base(predicate)
    { }

    private PersonSpecs(string command, params object[] parameters)
        : base(command, parameters)
    { }

    public static PersonSpecs GetPersonById(int personId)
    {
        return new PersonSpecs(p => p.PersonId == personId);

        /* command/parameters to execute stored procedure */
        /* return new PersonSpecs("EXEC GetPersonById @PersonId = {0}", personId); */
    }

    public static PersonSpecs GetAll()
    {
        return new PersonSpecs(p => true);
    }
}

// EntityFramework
public class DbContextQueryAdapter<T> : DelegateQueryAdapter<T> where T : EntityBase
{
    public DbContextQueryAdapter(DbContext dbContext)
        : base
        (
            /* read */
            (predicate) =>
            {
                return dbContext.Set<T>().Where(predicate);
            },

            /* read (command/parameters) */
            (command, parameters) =>
            {
                return dbContext.Database.SqlQuery<T>(command, parameters).AsQueryable();
            }
        )
    { }
}

```
</p>
</details>

</p>
</details>

## DataStore

DataStore class presents full CRUD operations.

**Usage**

```csharp

var dataStore = new DataStore<Person>(new SqlClientCrudPersonAdapter(/* connection string */));

// Add a Person    
dataStore.Add(new Person 
    {
        Firstname = "Adam",
        Lastname = "Sanchez"
    });
    
// Edit a Person
var personToEdit = dataStore.FindSingle(PersonSpecs.GetPersonById(1)); 
if (personToEdit != null)
{
    personToEdit.Firstname = "Eve";
    dataStore.Update(personToEdit);
}

// Delete a Person
var personToDelete = dataStore.FindSingle(PersonSpecs.GetPersonById(2)); 
if (personToDelete != null)
{
    dataStore.Delete(personToDelete);
}

```

<details><summary>See more</summary>
<p>

**InMemory**

```csharp

var dataStore = new DataStore<Person>(
    new DelegateCrudAdapter<Person>(
            /* create */
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

            /* update */
            (e) =>
            {
                var person = people.FirstOrDefault(p => p.PersonId == e.PersonId);
                if (person != null)
                {
                    person.Firstname = e.Firstname;
                    person.Lastname = e.Lastname;
                }
            },

            /* delete */
            (e) =>
            {
                var person = people.FirstOrDefault(p => p.PersonId == e.PersonId);
                if (person != null)
                {
                    people.Remove(person);
                }
            },

            /* read */
            (predicate) =>
            {
                return people.Where(predicate.Compile()).AsQueryable();
            }
        )
    );

// Add a Person    
dataStore.Add(new Person 
    {
        Firstname = "Adam",
        Lastname = "Sanchez"
    });
    
// Edit a Person
var personToEdit = dataStore.FindSingle(PersonSpecs.GetPersonById(1)); 
if (personToEdit != null)
{
    personToEdit.Firstname = "Eve";
    dataStore.Update(personToEdit);
}

// Delete a Person
var personToDelete = dataStore.FindSingle(PersonSpecs.GetPersonById(2)); 
if (personToDelete != null)
{
    dataStore.Delete(personToDelete);
}

```

<details><summary><b>rest of the codes</b></summary>
<p>

```csharp

// SqlClient
public class SqlClientCrudPersonAdapter : DelegateCrudAdapter<Person>
{
    public SqlClientCrudPersonAdapter(string connectionString)
        : base
        (
            /* create */
            (e) =>
            {
                using (var connection = new SqlConnection(connectionString))
                {                           
                    var sql = "INSERT INTO dbo.People (Firstname, Lastname) VALUES (@Firstname, @Lastname)";
                    using (var command = new SqlCommand(sql, connection))
                    {
                        command.Parameters.Add("@Firstname", SqlDbType.VarChar, 50).Value = e.Firstname;
                        command.Parameters.Add("@Lastname", SqlDbType.VarChar, 50).Value = e.Lastname;
                        command.CommandType = CommandType.Text;

                        connection.Open();
                        command.ExecuteNonQuery();
                    }
                }
            },

            /* update */
            (e) =>
            {
                using (var connection = new SqlConnection(connectionString))
                {                           
                    var sql = "UPDATE dbo.People SET Firstname = @Firstname, Lastname = @Lastname WHERE PersonId = @PersonId";
                    using (var command = new SqlCommand(sql, connection))
                    {
                        command.Parameters.Add("@PersonId", SqlDbType.Int).Value = e.PersonId;
                        command.Parameters.Add("@Firstname", SqlDbType.VarChar, 50).Value = e.Firstname;
                        command.Parameters.Add("@Lastname", SqlDbType.VarChar, 50).Value = e.Lastname;
                        command.CommandType = CommandType.Text;

                        connection.Open();
                        command.ExecuteNonQuery();
                    }
                }
            },

            /* delete */
            (e) =>
            {
                using (var connection = new SqlConnection(connectionString))
                {                            
                    var sql = "DELETE dbo.People WHERE PersonId = @PersonId";
                    using (var command = new SqlCommand(sql, connection))
                    {
                        command.Parameters.Add("@PersonId", SqlDbType.Int).Value = e.PersonId;
                        command.CommandType = CommandType.Text;

                        connection.Open();
                        command.ExecuteNonQuery();
                    }
                }
            },

            /* read */
            (predicate) =>
            {
                var people = new List<Entities.Person>();
                using (var connection = new SqlConnection(connectionString))
                {
                    var sql = "SELECT PersonId, Lastname, Firstname FROM dbo.People";  // convert predicate to where clause
                    using (var command = new SqlCommand(sql, connection))
                    {
                        connection.Open();
                        using (SqlDataReader dr = command.ExecuteReader())
                        {
                            if (dr.HasRows)
                            {
                                while (dr.Read())
                                {
                                    people.Add(new Person
                                    {
                                        PersonId = (int)dr["PersonId"],
                                        Lastname = (string)dr["Lastname"],
                                        Firstname = (string)dr["Firstname"],
                                    });
                                }
                            }
                        }
                    }
                }

                return people.AsQueryable().Where(predicate);
            }
        )
    { }
}

```

</p>
</details>

</p>
</details>

## DataContextBase/UnitOfWorkBase

DataContextBase abstract class is a data storage container and handles underlying IUnitOfWork implementation.

UnitOfWorkBase abstract class is a simple UnitOfWork implementation with relational mapping support.

**Usage**

```csharp

// Add   
using (var context = DataContext.Factory())
{
    var person = new Entities.Person
    {
        Firstname = "Pauline",
        Lastname = "Koch",
        Identifications = new List<Identification>()
        {
            new Identification 
            {
                Type = Identification.Types.SSN,
                Number = "222-222-222"
            }
        }
    };

    context.Add(person);
    context.SaveChanges();
}

// Update
using (var context = DataContext.Factory())
{
    var person = context.FindSingle(PersonSpecs.GetPersonById(1));
    if (person != null)
    {
        person.Firstname = "Paul";

        person.Identifications.Add(
            new Identification
            {
                Type = Identification.Types.TIN,
                Number = "563-2352"
            });

        context.SaveChanges();
    }
}

// Delete
using (var context = DataContext.Factory())
{
    var person = context.FindSingle(PersonSpecs.GetPersonById(2));
    if (person != null)
    {
        context.Delete(person);
        context.SaveChanges();
    }
}

```

<details><summary>See more</summary>
<p>
    
DataContext as factory method to switch from UnitOfWorkDbContext and UnitOfWorkInMemory.

```csharp

public class DataContext : DataContextBase
{
    private DataContext(IUnitOfWork unitOfWork)
        : base(unitOfWork)
    {
    }

    public static DataContext Factory()
    {                
        /* DbContext as UnitOfWork */
        return new DataContext(new UnitOfWorkDbContext());
        
        /* return new DataContext(new UnitOfWorkInMemory()); */
    }
}

```

<details><summary><b>UnitOfWorkDbContext</b></summary>
<p>

EntityFramework DbContext with CrudDatastore's IUnitOfWork.

```csharp

/* EntityFramwork UnitOfWork - DbContext */
public class  UnitOfWorkDbContext : UnitOfWorkDbContextBase
{
    public UnitOfWorkDbContext()
        : base(/* connection string */)
    { }

    protected override void OnModelCreating(DbModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Entities.Person>()
            .HasMany(p => p.Identifications)
            .WithOptional()
            .HasForeignKey(i => i.PersonId);

        modelBuilder.Entity<Entities.Person>()
            .ToTable("People");

        modelBuilder.Entity<Entities.Identification>()
            .ToTable("Identifications");

        base.OnModelCreating(modelBuilder);
    }
}

public abstract class UnitOfWorkDbContextBase : DbContext, IUnitOfWork
{
    private readonly IDictionary<Type, object> _dataQueries = new Dictionary<Type, object>();

    public event EventHandler<EntityEventArgs> EntityMaterialized;
    public event EventHandler<EntityEventArgs> EntityCreate;
    public event EventHandler<EntityEventArgs> EntityUpdate;
    public event EventHandler<EntityEventArgs> EntityDelete;

    public UnitOfWorkDbContextBase(string connection)
        : base(connection)
    {
        Database.SetInitializer<UnitOfWorkDbContextBase>(null);
        ((IObjectContextAdapter)this).ObjectContext.ObjectMaterialized += (sender, e) => EntityMaterialized?.Invoke(this, new EntityEventArgs(e.Entity));
    }

    public IDataQuery<T> Read<T>() where T : EntityBase
    {
        var entityType = typeof(T);
        if (_dataQueries.ContainsKey(entityType))
            return (IDataQuery<T>)_dataQueries[entityType];

        var dataQuery = new DataQuery<T>(new DbContextQueryAdapter<T>(this));
        _dataQueries.Add(entityType, dataQuery);

        return dataQuery;
    }

    public void MarkNew<T>(T entity) where T : EntityBase
    {
        Set<T>().Add(entity);
    }

    public void MarkModified<T>(T entity) where T : EntityBase
    {
        var entry = Entry(entity);
        if (entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
            entry.State = EntityState.Modified;
    }

    public void MarkDeleted<T>(T entity) where T : EntityBase
    {
        var entry = Entry(entity);
        if (entry.State == EntityState.Detached)
            Set<T>().Attach(entity);

        Set<T>().Remove(entity);
    }

    public void Commit()
    {
        ChangeTracker.DetectChanges();

        foreach (var entry in ChangeTracker.Entries().Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted))
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    EntityCreate?.Invoke(this, new EntityEventArgs(entry.Entity));
                    break;
                case EntityState.Modified:
                    EntityUpdate?.Invoke(this, new EntityEventArgs(entry.Entity));
                    break;
                case EntityState.Deleted:
                    EntityDelete?.Invoke(this, new EntityEventArgs(entry.Entity));
                    break;
            }
        }

        SaveChanges();
    }
}

```
</p>
</details>

<details><summary><b>UnitOfWorkInMemory</b></summary>
<p>
    
UnitOfWorkInMemory with related property mapping.    

One caveat on the Datastore's DelegateCrudAdapter IQueryable&lt;EntityBase&gt; returns, it should return unique objects to properly trace changes to the entity objects.

```csharp

public class Person : EntityBase
{
    public int PersonId { get; set; }
    public string Firstname { get; set; }
    public string Lastname { get; set; }
    public virtual ICollection<Identification> Identifications { get; set; }
}

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

public class UnitOfWorkInMemory : UnitOfWorkBase
{
    public UnitOfWorkInMemory()
    {
        var people = new List<Person>
        {
            new Person { PersonId = 1, Firstname = "Hermann", Lastname = "Einstein "},
            new Person { PersonId = 2, Firstname = "Albert", Lastname = "Einstein "},
            new Person { PersonId = 3, Firstname = "Maja", Lastname = "Einstein "}
        };

        var identifications = new List<Identification>
        {
            new Identification { IdentificationId = 1, PersonId = 1, Type = Entities.Identification.Types.SSN, Number = "509–515-224" },
            new Identification { IdentificationId = 2, PersonId = 1, Type = Entities.Identification.Types.TIN, Number = "92–4267" },
            new Identification { IdentificationId = 3, PersonId = 2, Type = Entities.Identification.Types.SSN, Number = "425–428-336" },
        };

        var dataStorePerson = new DataStore<Person>(
            new DelegateCrudAdapter<Person>(
                /* create */
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

                /* update */
                (e) =>
                {
                    var person = people.FirstOrDefault(p => p.PersonId == e.PersonId);
                    if (person != null)
                    {
                        person.Firstname = e.Firstname;
                        person.Lastname = e.Lastname;
                    }
                },

                /* delete */
                (e) =>
                {
                    var person = people.FirstOrDefault(p => p.PersonId == e.PersonId);
                    if (person != null)
                    {
                        people.Remove(person);
                    }
                },

                /* read */
                (predicate) =>
                {         
                    return people.Where(predicate.Compile()).AsQueryable();
                }
            )
        );

        var dataStoreIdentification = new DataStore<Identification>(
            new DelegateCrudAdapter<Identification>(
                /* create */
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

                /* update */
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

                /* delete */
                (e) =>
                {
                    var identification = identifications.FirstOrDefault(i => i.IdentificationId == e.IdentificationId);
                    if (identification != null)
                    {
                        identifications.Remove(identification);
                    }
                },

                /* read */
                (predicate) =>
                {
                    return identifications.Where(predicate.Compile()).AsQueryable();
                }
            )
        );

        /* Data Registration and Relational Mapping */
        this.Register(dataStorePerson)
            .Map(p => p.Identifications, (p, i) => p.PersonId == i.PersonId);
        this.Register(dataStoreIdentification);
    }
}

```

</p>
</details>

</p>
</details>

<p align="center"><b>Happy Coding!</b></p>
