# CrudDatastore - Simple Data Access Framework

CrudDatastore is a data access framework that uses the Specification pattern to query data with LINQ expressions or command/parameter execution.

It provides `DataQuery` and `DataStore` as simple query and CRUD entry points.

For richer scenarios, use `DataContextBase` and `UnitOfWorkBase` as a data context and unit-of-work foundation with relational mapping support.

## Installation

Use NuGet to install the package:

```powershell
PM> Install-Package CrudDatastore -Version 2.0.0-preview.1
```

Or with dotnet CLI:

```bash
dotnet add package CrudDatastore --version 2.0.0-preview.1
```

## DataQuery

`DataQuery` is focused on read/query operations using specifications.

**Usage**

```csharp
var dataQuery = new DataQuery<Person>(new DelegateQueryAdapter<Person>(
    predicate => people.Where(predicate.Compile()).AsQueryable(),
    (command, parameters) => people.AsQueryable()
));

var person = dataQuery.FindSingle(PersonSpecs.GetById(1));
var allPeople = dataQuery.Find(PersonSpecs.GetAll()).ToList();
```

Specification example:

```csharp
public class PersonSpecs : Specification<Person>
{
    private PersonSpecs(Expression<Func<Person, bool>> predicate) : base(predicate) { }

    public static PersonSpecs GetById(int id) => new PersonSpecs(p => p.PersonId == id);
    public static PersonSpecs GetAll() => new PersonSpecs(p => true);
}
```

## DataStore

`DataStore` provides full CRUD operations.

**Usage**

```csharp
var dataStore = new DataStore<Person>(new DelegateCrudAdapter<Person>(
    create: entity => people.Add(entity),
    update: entity =>
    {
        var existing = people.FirstOrDefault(p => p.PersonId == entity.PersonId);
        if (existing != null)
        {
            existing.Firstname = entity.Firstname;
            existing.Lastname = entity.Lastname;
        }
    },
    delete: entity =>
    {
        var existing = people.FirstOrDefault(p => p.PersonId == entity.PersonId);
        if (existing != null)
            people.Remove(existing);
    },
    read: predicate => people.Where(predicate.Compile()).AsQueryable()
));

// create
var person = new Person { PersonId = 10, Firstname = "Adam", Lastname = "Sanchez" };
dataStore.Add(person);

// update
person.Firstname = "Eve";
dataStore.Update(person);

// delete
dataStore.Delete(person);
```

## DataContextBase / UnitOfWorkBase

Use `DataContextBase` and `UnitOfWorkBase` when you need a context-oriented unit-of-work flow.

**Usage**

```csharp
using (var context = DataContext.Factory())
{
    var person = new Person
    {
        Firstname = "Pauline",
        Lastname = "Koch"
    };

    context.Add(person);
    context.SaveChanges();
}

using (var context = DataContext.Factory())
{
    var person = context.FindSingle(PersonSpecs.GetById(1));
    if (person != null)
    {
        person.Firstname = "Paul";
        context.SaveChanges();
    }
}

using (var context = DataContext.Factory())
{
    var person = context.FindSingle(PersonSpecs.GetById(2));
    if (person != null)
    {
        context.Delete(person);
        context.SaveChanges();
    }
}
```

Happy Coding!
