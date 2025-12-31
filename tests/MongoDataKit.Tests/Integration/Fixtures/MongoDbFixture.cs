using Mongo2Go;
using MongoDB.Driver;
using Xunit;

namespace MongoDataKit.Tests.Integration.Fixtures;

/// <summary>
/// Fixture that provides an in-memory MongoDB instance for integration tests.
/// Uses Mongo2Go to spin up a temporary MongoDB server.
/// </summary>
public class MongoDbFixture : IDisposable
{
    private readonly MongoDbRunner _runner;

    public IMongoClient Client { get; }
    public IMongoDatabase Database { get; }
    public string ConnectionString { get; }
    public string DatabaseName { get; } = "MongoDataKit_IntegrationTests";

    public MongoDbFixture()
    {
        // Start an in-memory MongoDB instance
        _runner = MongoDbRunner.Start();
        ConnectionString = _runner.ConnectionString;
        Client = new MongoClient(ConnectionString);
        Database = Client.GetDatabase(DatabaseName);
    }

    /// <summary>
    /// Gets a collection for testing.
    /// </summary>
    public IMongoCollection<T> GetCollection<T>(string name)
    {
        return Database.GetCollection<T>(name);
    }

    /// <summary>
    /// Creates a fresh collection, dropping any existing one.
    /// </summary>
    public IMongoCollection<T> GetFreshCollection<T>(string name)
    {
        Database.DropCollection(name);
        return Database.GetCollection<T>(name);
    }

    public void Dispose()
    {
        _runner?.Dispose();
    }
}

/// <summary>
/// Collection definition for sharing the MongoDB fixture across tests.
/// </summary>
[CollectionDefinition(nameof(MongoDbCollection))]
public class MongoDbCollection : ICollectionFixture<MongoDbFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
