# MongoDataKit

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)

A lightweight, powerful .NET library for MongoDB that simplifies database operations with built-in support for indexing, pagination, transactions, and the repository pattern.

> **Note:** This is a class library project, not a NuGet package. You can reference the projects directly in your solution or create your own NuGet package from the source code.

## ✨ Features

- **Automatic Index Management** - Create single, compound, unique, TTL, geospatial, text, and case-insensitive indexes via configuration
- **Pagination** - Efficient facet-based aggregation pagination
- **Repository Pattern** - Clean abstraction with Unit of Work support
- **Transaction Support** - Automatic transaction handling for replica sets
- **Fluent Filtering** - Type-safe query building
- **Soft Delete** - Mark documents as deleted without permanent removal, with automatic query filtering
- **Audit Trail** - Automatic tracking of CreatedAt/CreatedBy and ModifiedAt/ModifiedBy fields
- **Optimistic Concurrency** - Version-based conflict detection to prevent lost updates
- **Change Streams** - Real-time data change notifications for reactive applications
- **Text Search** - Full-text search with relevance scoring and multi-field support
- **Schema Validation** - Enforce document structure with JSON Schema validation

## 📦 Installation

### Option 1: Reference as Project (Recommended)

Clone the repository and add project references to your solution:

```bash
git clone https://github.com/dhananjayupadhyay/mongo-data-kit.git
```

Add project references in your `.csproj`:

```xml
<ItemGroup>
  <ProjectReference Include="..\path\to\MongoDataKit.Core\MongoDataKit.Core.csproj" />
  <ProjectReference Include="..\path\to\MongoDataKit.Persistence\MongoDataKit.Persistence.csproj" />
  <ProjectReference Include="..\path\to\MongoDataKit.Initializer\MongoDataKit.Initializer.csproj" />
</ItemGroup>
```

### Option 2: Create Your Own NuGet Package

You can package the library yourself using:

```bash
dotnet pack -c Release
```

This will generate `.nupkg` files in the `bin/Release` folder that you can publish to your private NuGet feed or NuGet.org.

## 🚀 Quick Start

### 1. Configure MongoDB Settings

```json
{
  "MongoDb": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "myapp",
    "SupportsTransactions": false,
    "Collections": {
      "users": {
        "Indexes": {
          "ix_email_unique": {
            "Fields": [
              { "PropertyName": "email", "SortDirection": "Ascending" }
            ],
            "Unique": true
          }
        }
      }
    }
  }
}
```

### 2. Register Services

```csharp
builder.Services.Configure<MongoSettings>(
    builder.Configuration.GetSection("MongoDb"));

builder.Services.AddSingleton<IMongoClient>(sp =>
    new MongoClient(builder.Configuration["MongoDb:ConnectionString"]));

builder.Services.AddMongoInitializer();
builder.Services.AddScoped<IMongoDbContext, MongoDbContext>();
builder.Services.AddScoped<IUnitOfWork, MongoUnitOfWork>();
```

### 3. Initialize Database

```csharp
var initializer = app.Services.GetRequiredService<IDatabaseInitializer>();
await initializer.InitializeAsync();
```

---

## 📖 Documentation

### Index Types

#### Standard Index

```json
{
  "ix_name": {
    "Fields": [{ "PropertyName": "name", "SortDirection": "Ascending" }]
  }
}
```

#### Unique Index

```json
{
  "ix_email_unique": {
    "Fields": [{ "PropertyName": "email" }],
    "Unique": true
  }
}
```

#### Compound Index

```json
{
  "ix_name_date": {
    "Fields": [
      { "PropertyName": "lastName", "SortDirection": "Ascending" },
      { "PropertyName": "createdAt", "SortDirection": "Descending" }
    ]
  }
}
```

#### TTL Index (Auto-Expiring Documents)

```json
{
  "ix_session_ttl": {
    "Fields": [{ "PropertyName": "expiresAt" }],
    "Ttl": "01:00:00"
  }
}
```

> **Note:** TTL indexes only work on `DateTime` fields. MongoDB's background process may take up to 60 seconds to delete expired documents.

#### Geospatial Index

```json
{
  "ix_location_geo": {
    "Fields": [{ "PropertyName": "location", "IndexKind": "Geo2dSphere" }]
  }
}
```

#### Case-Insensitive Index

```json
{
  "ix_name_ci": {
    "Fields": [{ "PropertyName": "name" }],
    "CaseInsensitive": true
  }
}
```

---

### Creating Entities

```csharp
public class User : MongoEntity<string>
{
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

---

### Repository Implementation

```csharp
public interface IUserRepository : IMongoRepository<User, string> { }

public class UserRepository : MongoRepository<User, string>, IUserRepository
{
    public UserRepository(IMongoDbContext context, IMongoDatabase database)
        : base(context, database.GetCollection<User>("users")) { }
}
```

---

### CRUD Operations

```csharp
// Using Unit of Work (batched)
public async Task CreateUsersAsync(IEnumerable<User> users)
{
    _userRepository.Add(users);
    await _unitOfWork.CommitAsync();
}

// Using Session (immediate)
public async Task<User> UpdateUserAsync(User user)
{
    return await _userRepository.WithTransactionAsync(async session =>
    {
        await _userRepository.UpdateAsync(session, user);
        return user;
    });
}
```

---

### Pagination

```csharp
public class UserFilter : IQueryFilter<User>
{
    public int PageSize { get; set; } = 20;
    public int Skip { get; set; } = 0;
    public TimeSpan MaxExecutionTime { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan MaxAwaitTime { get; set; } = TimeSpan.FromSeconds(30);

    public string? NameContains { get; set; }

    public FilterDefinition<User> ToFilterDefinition()
    {
        if (string.IsNullOrEmpty(NameContains))
            return Builders<User>.Filter.Empty;

        return Builders<User>.Filter.Regex(
            x => x.Name,
            new BsonRegularExpression(NameContains, "i"));
    }

    public SortDefinition<User> ToSortDefinition()
        => Builders<User>.Sort.Descending(x => x.CreatedAt);
}

// Usage
var filter = new UserFilter { NameContains = "john", PageSize = 10 };
var result = await _userRepository.FindPagedAsync(filter);
// result.Items, result.TotalCount, result.PageSize, result.Skip
```

---

### Transactions

```csharp
// Automatic transaction handling
var result = await _repository.WithTransactionAsync(async session =>
{
    var user = await _repository.GetByIdAsync(session, userId);
    user.Name = "Updated Name";
    await _repository.UpdateAsync(session, user);
    return user;
});
```

---

### Soft Delete

Use `SoftDeleteEntity` or `FullFeaturedEntity` base class, and `TrackedRepository` for automatic filtering.

```csharp
// Entity with soft delete support
public class Product : SoftDeleteEntity<string>
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

// Repository automatically filters out deleted items
public class ProductRepository : TrackedRepository<Product, string>, IProductRepository
{
    public ProductRepository(IMongoDbContext context, IMongoDatabase database, IAuditContext auditContext)
        : base(context, database.GetCollection<Product>("products"), auditContext) { }
}

// Usage
_repository.SoftDelete(productId);     // Marks as deleted (keeps data)
_repository.Restore(productId);        // Restores soft-deleted item
await _repository.GetAllAsync();       // Only returns non-deleted items
await _repository.GetAllIncludingDeletedAsync(); // Returns all items
```

---

### Audit Trail

Entities implementing `IAuditable` automatically track creation and modification times.

```csharp
// Entity with audit fields
public class Order : AuditableEntity<string>
{
    public string CustomerId { get; set; } = string.Empty;
    public decimal Total { get; set; }
}

// Register your audit context to track user identity
public class HttpContextAuditContext : IAuditContext
{
    private readonly IHttpContextAccessor _accessor;
    public HttpContextAuditContext(IHttpContextAccessor accessor) => _accessor = accessor;
    public string? CurrentUserId => _accessor.HttpContext?.User?.Identity?.Name;
}

// In Program.cs
builder.Services.AddAuditContext<HttpContextAuditContext>();

// Fields are automatically populated:
// - CreatedAt, CreatedBy: Set on Add()
// - ModifiedAt, ModifiedBy: Set on Update()
```

---

### Optimistic Concurrency

Entities implementing `IVersioned` get automatic version checking on updates.

```csharp
// Entity with version control
public class Account : VersionedEntity<string>
{
    public decimal Balance { get; set; }
}

// Concurrent update protection
try
{
    var account = await _repository.GetByIdAsync(accountId);
    account.Balance -= 100;
    await _repository.UpdateAsync(session, account);
}
catch (ConcurrencyException ex)
{
    // Another process modified the account - handle conflict
    Console.WriteLine($"Conflict on version {ex.ExpectedVersion}");
}
```

> **How it works:** The repository checks `WHERE _id = X AND Version = originalVersion`. If no match, the document was modified by another process and `ConcurrencyException` is thrown.

---

### Entity Base Classes

Choose the right base class for your needs:

| Base Class                | Features                              |
| ------------------------- | ------------------------------------- |
| `MongoEntity<TId>`        | Basic entity with ID                  |
| `AuditableEntity<TId>`    | + CreatedAt/By, ModifiedAt/By         |
| `SoftDeleteEntity<TId>`   | + Auditable + IsDeleted, DeletedAt/By |
| `VersionedEntity<TId>`    | + Auditable + Version control         |
| `FullFeaturedEntity<TId>` | All features combined                 |

---

## 🔄 Change Streams

Change Streams allow you to listen for real-time changes in your MongoDB collections. Perfect for event-driven architectures, real-time dashboards, and cache invalidation.

> **Note:** Change Streams require a MongoDB replica set or sharded cluster. They won't work with standalone MongoDB instances.

### Basic Usage

```csharp
using MongoDataKit.Accessors.ChangeStreams;

// Create a watcher for a collection
var watcher = collection.CreateWatcher();

// Watch for all changes
await foreach (var change in watcher.WatchAsync(cancellationToken))
{
    Console.WriteLine($"Operation: {change.OperationType}");
    Console.WriteLine($"Document ID: {change.DocumentKey}");

    if (change.FullDocument != null)
        Console.WriteLine($"Data: {change.FullDocument.Name}");
}
```

### Change Event Properties

| Property            | Description                                       |
| ------------------- | ------------------------------------------------- |
| `OperationType`     | Insert, Update, Replace, Delete, Drop, etc.       |
| `FullDocument`      | The complete document (for insert/update/replace) |
| `DocumentKey`       | The `_id` of the affected document                |
| `ResumeToken`       | Token to resume watching from this point          |
| `Timestamp`         | When the change occurred                          |
| `UpdateDescription` | Fields that were updated/removed                  |

### Watch with Custom Options

```csharp
var options = new ChangeStreamOptions
{
    FullDocument = FullDocumentOption.UpdateLookup, // Get full document on updates
    MaxAwaitTime = TimeSpan.FromSeconds(30),
    BatchSize = 100
};

await foreach (var change in watcher.WatchAsync(options, cancellationToken))
{
    // Process change
}
```

### Resume After Disconnection

```csharp
BsonDocument? lastToken = null;

await foreach (var change in watcher.WatchAsync(cancellationToken))
{
    lastToken = change.ResumeToken; // Save this to resume later
    ProcessChange(change);
}

// Later, resume from where you left off
var options = new ChangeStreamOptions { ResumeAfter = lastToken };
await foreach (var change in watcher.WatchAsync(options, cancellationToken))
{
    // Continue processing
}
```

### Use Cases

| Use Case                  | Description                             |
| ------------------------- | --------------------------------------- |
| **Real-time Dashboards**  | Update UI immediately when data changes |
| **Cache Invalidation**    | Clear cached data when source changes   |
| **Audit Logging**         | Log every change for compliance         |
| **Event Sourcing**        | Trigger business events on data changes |
| **Sync to Search Engine** | Keep Elasticsearch/Algolia in sync      |

---

## 🔍 Text Search

MongoDB's text search enables full-text search capabilities with relevance scoring. MongoDataKit provides convenient extension methods for common search operations.

> **Important:** Text search requires a text index on the collection. See [Text Index Configuration](#text-index) below.

### Basic Text Search

```csharp
using MongoDataKit.Accessors.Search;

// Search across all text-indexed fields
var results = await collection.TextSearchAsync("mongodb tutorial");
// Returns: Documents containing "mongodb" or "tutorial", sorted by relevance
```

### Search with Relevance Scores

```csharp
// Get documents with their relevance scores
var results = await collection.TextSearchWithScoreAsync("database performance");

foreach (var result in results)
{
    Console.WriteLine($"{result.Document.Title}: {result.Score:F2}");
}
// Output:
// "Optimizing Database Performance": 1.50
// "Database Indexing Guide": 1.25
// "MongoDB Best Practices": 0.75
```

### Search Options

```csharp
var options = new TextSearchOptions
{
    Language = "english",           // Stemming language
    CaseSensitive = false,          // Case-insensitive (default)
    DiacriticSensitive = false,     // Ignore accents (default)
    Limit = 20,                     // Max results
    Skip = 0,                       // For pagination
    SortByScore = true              // Sort by relevance (default)
};

var results = await collection.TextSearchAsync("search terms", options);
```

### Field-Specific Regex Search

For partial matching (when you don't have a text index):

```csharp
// Search for partial matches in a specific field
var results = await collection.SearchFieldAsync(
    fieldName: "email",
    searchText: "@company.com",
    caseInsensitive: true,
    limit: 50
);
```

### Count Matching Documents

```csharp
var count = await collection.TextSearchCountAsync("mongodb");
Console.WriteLine($"Found {count} matching documents");
```

### Text Search Operators

| Operator | Example         | Matches                           |
| -------- | --------------- | --------------------------------- |
| Space    | `coffee shop`   | Documents with "coffee" OR "shop" |
| Quotes   | `"coffee shop"` | Exact phrase "coffee shop"        |
| Hyphen   | `coffee -decaf` | "coffee" but NOT "decaf"          |

```csharp
// Exact phrase search
var results = await collection.TextSearchAsync("\"machine learning\"");

// Exclude terms
var results = await collection.TextSearchAsync("python -snake");

// Combined
var results = await collection.TextSearchAsync("\"data science\" tensorflow -keras");
```

### Text Index

Configure text indexes in `appsettings.json`:

```json
{
  "MongoDb": {
    "Collections": {
      "articles": {
        "Indexes": {
          "ix_content_text": {
            "Fields": [
              { "PropertyName": "title", "IndexKind": "Text" },
              { "PropertyName": "content", "IndexKind": "Text" },
              { "PropertyName": "tags", "IndexKind": "Text" }
            ],
            "TextLanguage": "english",
            "TextWeights": {
              "title": 10,
              "content": 5,
              "tags": 2
            }
          }
        }
      }
    }
  }
}
```

> **Weights:** Higher weight = more relevance. In the example above, matches in `title` are 5x more important than `tags`.

---

## 📋 Schema Validation

MongoDB Schema Validation ensures documents conform to a defined structure. MongoDataKit allows you to configure validation rules via settings that are applied during database initialization.

### Why Use Schema Validation?

| Benefit                   | Description                                         |
| ------------------------- | --------------------------------------------------- |
| **Data Integrity**        | Prevent invalid data from being inserted            |
| **Documentation**         | Schema serves as documentation for your data model  |
| **Early Error Detection** | Catch errors at insert time, not query time         |
| **Gradual Migration**     | Use "moderate" level to validate only new documents |

### Configuration

Add validation to your collection settings in `appsettings.json`:

```json
{
  "MongoDb": {
    "Collections": {
      "users": {
        "Validation": {
          "Level": "Strict",
          "Action": "Error",
          "JsonSchema": {
            "bsonType": "object",
            "required": ["name", "email"],
            "properties": {
              "name": {
                "bsonType": "string",
                "minLength": 1,
                "maxLength": 100,
                "description": "User's full name - required"
              },
              "email": {
                "bsonType": "string",
                "pattern": "^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}$",
                "description": "Valid email address - required"
              },
              "age": {
                "bsonType": "int",
                "minimum": 0,
                "maximum": 150,
                "description": "Age must be between 0 and 150"
              },
              "status": {
                "enum": ["active", "inactive", "pending"],
                "description": "Must be one of: active, inactive, pending"
              }
            }
          }
        }
      }
    }
  }
}
```

### Validation Levels

| Level      | Behavior                                                                    |
| ---------- | --------------------------------------------------------------------------- |
| `Strict`   | Validate ALL inserts and updates                                            |
| `Moderate` | Only validate inserts and updates to documents that already pass validation |
| `Off`      | Disable validation                                                          |

### Validation Actions

| Action  | Behavior                              |
| ------- | ------------------------------------- |
| `Error` | Reject documents that fail validation |
| `Warn`  | Allow documents but log a warning     |

### Common Schema Patterns

#### Required Fields with Types

```json
{
  "bsonType": "object",
  "required": ["name", "email", "createdAt"],
  "properties": {
    "name": { "bsonType": "string" },
    "email": { "bsonType": "string" },
    "createdAt": { "bsonType": "date" }
  }
}
```

#### Nested Objects

```json
{
  "properties": {
    "address": {
      "bsonType": "object",
      "required": ["street", "city"],
      "properties": {
        "street": { "bsonType": "string" },
        "city": { "bsonType": "string" },
        "zipCode": { "bsonType": "string" }
      }
    }
  }
}
```

#### Arrays with Item Validation

```json
{
  "properties": {
    "tags": {
      "bsonType": "array",
      "minItems": 1,
      "maxItems": 10,
      "items": { "bsonType": "string" }
    }
  }
}
```

#### Enum Values

```json
{
  "properties": {
    "priority": {
      "enum": ["low", "medium", "high", "critical"]
    }
  }
}
```

### What Happens on Validation Failure?

When a document fails validation with `Action: Error`:

```csharp
try
{
    await collection.InsertOneAsync(invalidDocument);
}
catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.Uncategorized)
{
    // Document validation failed
    Console.WriteLine($"Validation error: {ex.WriteError.Message}");
}
```

---

## 🐳 Local Development with Docker

### Simple Standalone Instance

```bash
# Pull and run MongoDB
docker run -d -p 27017:27017 --name mongodb-local mongodb/mongodb-atlas-local

# Verify container is running
docker ps

# Stop and remove when done
docker stop mongodb-local && docker rm mongodb-local
```

### Replica Set Cluster (for Transactions)

For testing transaction support, use the provided docker-compose:

```bash
cd docker/cluster
docker-compose up -d
```

| Service       | Port  | Description    |
| ------------- | ----- | -------------- |
| mongo1        | 27018 | Primary node   |
| mongo2        | 27019 | Secondary node |
| mongo3        | 27020 | Secondary node |
| mongo-express | 8082  | Web UI admin   |

**Connection String:**

```
mongodb://localhost:27018,localhost:27019,localhost:27020/?replicaSet=mongo-replica-set
```

> **Note:** Cluster initialization takes ~30 seconds. Check logs with `docker logs mongodatakit_mongo1`

**Cleanup:**

```bash
docker-compose down -v
```

---

## 🧪 Running Tests

```bash
# Unit tests only
dotnet test --filter "Category=Unit"

# Integration tests (requires MongoDB)
dotnet test --filter "Category=Integration"

# All tests
dotnet test
```

---

## 📁 Project Structure

```
MongoDataKit/
├── src/
│   ├── MongoDataKit.Core/           # Configuration, paging, interfaces
│   ├── MongoDataKit.Accessors/      # Collection extensions, filtering
│   ├── MongoDataKit.Initializer/    # Database/index initialization
│   ├── MongoDataKit.Persistence/    # Repository, UoW, context
│   └── MongoDataKit.Abstractions/   # Base interfaces and entities
└── tests/
    └── MongoDataKit.Tests/          # Unit and integration tests
```

---

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## 🙏 Acknowledgments

- [MongoDB .NET Driver](https://github.com/mongodb/mongo-csharp-driver)
