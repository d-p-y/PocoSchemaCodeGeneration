# PocoSchemaCodeGeneration

## Why?

I like AsyncPoco's approach to generate poco classes from supported database. I was missing T4 support in dotnet core. This library in conjuction with [dotnet-script](https://github.com/filipw/dotnet-script) can be used to generate poco classes easily.

## Example usage with dotnet-script and postgresql

```
#! "netcoreapp2.1"

#r "nuget: System.Runtime.CompilerServices.Unsafe, 4.5.1"
#r "nuget: Npgsql, 4.0.3"
#r "nuget: PocoSchemaCodeGeneration.ForAsyncPoco, 1.1.0"

GenerateCode(new GeneratorSettings {
    CsFile = "../path-to-outcome-cs-file.cs",
    ConnectionString = @"Host=localhost;Username=some-user;Password=some-passwd;Database=put-db-name-here",
    DbProvider = Npgsql.NpgsqlFactory.Instance,
    SqlDialect = Dialect.Postgres,
    Namespace = "Some.NameSpace"
});
    
Console.WriteLine("ok");
```

## Example usage with dotnet-script and SQL Server

```
#! "netcoreapp2.1"

//uses https://github.com/filipw/dotnet-script

#r "nuget: System.Runtime.CompilerServices.Unsafe, 4.5.1"
#r "nuget: System.Data.SqlClient, 4.6.1"
#r "nuget: PocoSchemaCodeGeneration.ForAsyncPoco, 1.1.0"

using PocoSchemaCodeGeneration.ForAsyncPoco;

Generator.GenerateCode(new GeneratorSettings {
    CsFile = "Database.cs",
    ConnectionString = @"Data Source=localhost\SQLEXPRESS;Initial Catalog=dbname;Integrated security=true",
    DbProvider = System.Data.SqlClient.SqlClientFactory.Instance,
    SqlDialect = Dialect.SqlServer,
    Namespace = "NameSpace.Here"});
    
Console.WriteLine("ok");
```

## Original author
Github doesn't permit users to do multiple forks of the same source repo into the same destination account.
That's why ```Initial "fork" import``` commit should be treated as pseudo fork of:
- repo: https://github.com/tmenier/AsyncPoco
- commit hash: [4602d3f8b496f622b7a131e3be34cacbb0b915ba](https://github.com/tmenier/AsyncPoco/commit/4602d3f8b496f622b7a131e3be34cacbb0b915ba)
- with only two files imported:
  - AsyncPoco/AsyncPoco/T4 Templates/AsyncPoco.Core.ttinclude
  - AsyncPoco/AsyncPoco/T4 Templates/AsyncPoco.Generator.ttinclude  
