msbuild CrudDatastore.sln /p:Configuration=Release
nuget pack CrudDatastore/CrudDatastore.nuspec
nuget pack CrudDatastore/CrudDatastore.EntityFramework.nuspec