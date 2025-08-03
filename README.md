# TursoSharp

[![NuGet Version](https://img.shields.io/nuget/v/TursoSharp.svg)](https://www.nuget.org/packages/TursoSharp/) ![License](https://img.shields.io/badge/License-MIT-blue.svg)

C# bindings for [Turso Database](https://github.com/tursodatabase/turso) - a SQLite-compatible database engine written in Rust.

### **NOTE**:

**This project is experimental. I offer no support. If you wish to use this for something other than goofing around with a library, you should probably fork it.**

This project creates a wrapper into Turso using [csbindgen](https://github.com/Cysharp/csbindgen/). The Rust code gets interoperated through pinvokes. I've written this to be as trimmable as possible; it should support NativeAOT.

This project was partially a way to write Rust. I have zero idea if it's good; I mostly cribbed it from the bindings within their repo, looking at GitHub, looking at some Rust books I have, and then using Claude to check my work. Is it good? Probably not, if anyone has good tips to make it cleaner or better you're more than welcome to PR into this repo to help! I would love to know.

## Installation

```bash
dotnet add package TursoSharp
```

The package comes baked in with native runtime libraries and targets for:

- Windows (x64)
- macOS (Universal)
- iOS (iOS, Mac Catalyst)
- Android
- Linux (x64)
- WASM (This will only work with the in-memory functions. If anyone knows if it's possible to do File IO in the Rust code please file a PR!)

You can disable these build in runtimes by setting `UseTursoNativeLibraries` to false as a build parameter.

### Todo App

The `samples` directory contains a Todo sample app, written with [Avalonia](https://avaloniaui.net/). This supports iOS, Android, and the Desktop. 

### TL;DR

```csharp
using TursoSharp;

// Create an in-memory database
using var database = TursoDatabase.OpenMemory();
using var connection = database.Connect();

// Execute SQL directly
connection.Execute("CREATE TABLE users (id INTEGER PRIMARY KEY, name TEXT, email TEXT)");
connection.Execute("INSERT INTO users (name, email) VALUES (?, ?)", "John Doe", "john@example.com");

// Query data
using var resultSet = connection.Query("SELECT * FROM users");
foreach (var row in resultSet)
{
    var id = row.GetInt32("id");
    var name = row.GetString("name");
    var email = row.GetString("email");
    Console.WriteLine($"User: {id}, {name}, {email}");
}
```

### File Database

```csharp
// Create or open a file database
using var database = TursoDatabase.OpenFile("myapp.db");
using var connection = database.Connect();
```


## Source Generators

There is a basic Roslyn source generator for doing simple ORM-ish things. This was mostly hacked together to reduce boilerplate for me.

### Entity Generator

Mark your classes with `[TursoEntity]` to automatically generate repository code:

```csharp
using TursoSharp.Attributes;

[TursoEntity(TableName = "users")]
public class User
{
    [TursoPrimaryKey]
    public int Id { get; set; }
    
    [TursoColumn(ColumnName = "user_name")]
    public string Name { get; set; } = "";
    
    public string Email { get; set; } = "";
    
    [TursoColumn(ColumnName = "created_at")]
    public DateTime CreatedAt { get; set; }
    
    [TursoColumn(ColumnName = "is_active")]
    public bool IsActive { get; set; } = true;
}
```

```csharp
using var database = TursoDatabase.OpenMemory();
using var connection = database.Connect();
using var userRepo = new UserRepository(connection);

// Create table
userRepo.CreateTable();

// Insert user
var userId = userRepo.Insert(new User
{
    Name = "Alice Johnson",
    Email = "alice@example.com",
    CreatedAt = DateTime.Now,
    IsActive = true
});

// Get user by ID
var user = userRepo.GetById(userId);

// Get all users
var allUsers = userRepo.GetAll();

// Update user
user.Email = "alice.johnson@example.com";
userRepo.Update(user);

// Delete user
userRepo.Delete(userId);

// Async operations
var usersAsync = await userRepo.GetAllAsync();
await userRepo.InsertAsync(newUser);
```

### Query Result Generator

Mark classes with `[TursoQueryResult]` to automatically generate query result mapping:

```csharp
using TursoSharp.Attributes;

[TursoQueryResult]
public class UserPostSummary
{
    [TursoQueryColumn(ColumnName = "user_id")]
    public int UserId { get; set; }
    
    [TursoQueryColumn(ColumnName = "user_name")]
    public string UserName { get; set; } = "";
    
    [TursoQueryColumn(ColumnName = "total_posts")]
    public int TotalPosts { get; set; }
    
    [TursoQueryColumn(ColumnName = "latest_post_date", IsOptional = true)]
    public DateTime? LatestPostDate { get; set; }
}
```

This generates extension methods for type-safe query result mapping:

```csharp
// Type-safe query execution
var sql = @"
    SELECT 
        u.id as user_id,
        u.user_name,
        COUNT(p.id) as total_posts,
        MAX(p.published_at) as latest_post_date
    FROM users u
    LEFT JOIN posts p ON u.id = p.user_id
    GROUP BY u.id, u.user_name";

// Generated extension methods
var summaries = connection.QueryUserPostSummary(sql);
var firstSummary = connection.QueryFirstUserPostSummary(sql);
var summaryOrDefault = connection.QueryFirstOrDefaultUserPostSummary(sql);

// Row-level mapping
using var resultSet = connection.Query(sql);
foreach (var row in resultSet)
{
    var summary = row.ToUserPostSummary();
    Console.WriteLine($"{summary.UserName}: {summary.TotalPosts} posts");
}

// Bulk mapping
var summaryList = resultSet.ToUserPostSummaryList();

// Async operations
var summariesAsync = await connection.QueryUserPostSummaryAsync(sql);
```

The source generator will make Async operators, but it's just a Task wrapping the normal query.

## Build source

```bash
# Build the Rust bindings first.
cd bindings
cargo build --release
cd ..

dotnet build src/TursoSharp.slnx

# Run tests
dotnet test src/TursoSharp.Tests/TursoSharp.Tests.csproj
```