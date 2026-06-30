# CrudDatastore

CrudDatastore is a datastore framework for .NET that provides flexible abstractions for querying, CRUD operations, and data access.

[![NuGet](https://img.shields.io/nuget/v/CrudDatastore)](https://www.nuget.org/packages/CrudDatastore)

---

## API at a glance

| Type | What it does |
|------|-------------|
| [`DataQuery<T>`](#dataquery) | Read-only queries via specifications |
| [`DataStore<T>`](#datastore) | Full CRUD via delegate adapters |
| [`DataContextBase` / `UnitOfWorkBase`](#datacontextbase--unitofworkbase) | Context-oriented unit-of-work with relational mapping support |

---

## Installation

```
PM> Install-Package CrudDatastore
```

```
dotnet add package CrudDatastore
```

---

## DataQuery

`DataQuery<T>` is focused on read/query operations via specifications.

`DelegateQueryAdapter<T>` accepts two delegates:
- **predicate** — receives a compiled LINQ expression; use for in-memory or ORM-style queries
- **command/parameters** — receives a raw SQL string and positional parameters; use for stored procedures or custom queries

```csharp
var dataQuery = new DataQuery<Person>(new DelegateQueryAdapter<Person>(
    /* predicate — LINQ-based query */
    predicate => context.People.Where(predicate.Compile()).AsQueryable(),

    /* command/parameters — raw SQL or stored procedure */
    (sql, parameters) => context.Database.FromSqlRaw(sql, parameters).AsQueryable()
));

// Using a lambda directly (routes through the predicate delegate)
var all    = dataQuery.Find(p => true).ToList();

// Using a Specification class (routes through the predicate delegate)
var all    = dataQuery.Find(PersonSpecs.GetAll()).ToList();

// Using a command Specification (routes through the command/parameters delegate)
var person = dataQuery.FindSingle(PersonSpecs.GetById(1));
var all    = dataQuery.Find(PersonSpecs.GetAll()).ToList();
```

**Specification class example**

```csharp
public class PersonSpecs : Specification<Person>
{
    private PersonSpecs(Expression<Func<Person, bool>> predicate) : base(predicate) { }
    private PersonSpecs(string command, params object[] parameters) : base(command, parameters) { }

    // LINQ predicate — resolved in-memory or by the ORM
    public static PersonSpecs GetAll() => new PersonSpecs(p => true);

    // Command/parameters — executes a stored procedure with a positional parameter
    public static PersonSpecs GetById(int id) => new PersonSpecs("EXEC sp_GetPersonById @0", id);
}
```

> **How routing works:** when a `Specification<T>` is built with a predicate expression it
> routes to the first delegate; when built with a command string and parameters it routes
> to the second. This lets you mix LINQ queries and stored procedures in the same
> `DataQuery<T>` without changing the call site.

---

## DataStore

`DataStore<T>` provides full CRUD on top of a `DelegateCrudAdapter<T>`.

```csharp
var dataStore = new DataStore<Person>(new DelegateCrudAdapter<Person>(
    create: entity => people.Add(entity),
    update: entity =>
    {
        var existing = people.FirstOrDefault(p => p.PersonId == entity.PersonId);
        if (existing != null)
        {
            existing.Firstname = entity.Firstname;
            existing.Lastname  = entity.Lastname;
        }
    },
    delete: entity =>
    {
        var existing = people.FirstOrDefault(p => p.PersonId == entity.PersonId);
        if (existing != null) people.Remove(existing);
    },
    read: predicate => people.Where(predicate.Compile()).AsQueryable()
));

// Create
var person = new Person { PersonId = 10, Firstname = "Adam", Lastname = "Sanchez" };
dataStore.Add(person);

// Update
person.Firstname = "Eve";
dataStore.Update(person);

// Delete
dataStore.Delete(person);
```

---

## DataContextBase / UnitOfWorkBase

Use `DataContextBase` and `UnitOfWorkBase` when you need a context-oriented
unit-of-work flow with optional relational (navigation property) mapping.

**Unit of work setup**

Register datastores in `UnitOfWorkBase`, then optionally map navigation properties
between them:

```csharp
public class MyUnitOfWork : UnitOfWorkBase
{
    public MyUnitOfWork()
    {
        var people          = new List<Person>();
        var identifications = new List<Identification>();

        var personStore = people.CreateDataStore(p => p.PersonId);
        var idStore     = identifications.CreateDataStore(i => i.IdentificationId);

        this.Register(personStore)
            .Map(p => p.Identifications, (p, i) => p.PersonId == i.PersonId); // navigation property

        this.Register(idStore);
    }
}

public class DataContext : DataContextBase
{
    private DataContext(IUnitOfWork unitOfWork) : base(unitOfWork) { }

    public static DataContext Factory() => new DataContext(new MyUnitOfWork());
}
```

**CRUD**

```csharp
// Create
using (var context = DataContext.Factory())
{
    var person = new Person { Firstname = "Pauline", Lastname = "Koch" };
    context.Add(person);
    context.SaveChanges();
}

// Read
using (var context = DataContext.Factory())
{
    var person = context.FindSingle<Person>(p => p.PersonId == 1);
}

// Update
using (var context = DataContext.Factory())
{
    var person = context.FindSingle<Person>(p => p.PersonId == 1);
    person.Firstname = "Paul";
    context.SaveChanges();
}

// Delete
using (var context = DataContext.Factory())
{
    var person = context.FindSingle<Person>(p => p.PersonId == 2);
    context.Delete(person);
    context.SaveChanges();
}
```

See **[CrudDatastore.Samples](https://github.com/tyronevergil/CrudDatastore.Samples)** for
complete examples covering SQL Server, Oracle, navigation properties, and multi-database units of work.
