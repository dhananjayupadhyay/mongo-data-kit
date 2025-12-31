using AutoFixture;
using AutoFixture.Xunit2;
using FluentAssertions;
using MongoDB.Driver;
using MongoDataKit.Core.Interfaces;
using MongoDataKit.Persistence;
using MongoDataKit.Persistence.Entities;
using MongoDataKit.Persistence.Repositories;
using MongoDataKit.Tests.Integration.Fixtures;
using Xunit;

namespace MongoDataKit.Tests.Integration.Repositories;

[Collection(nameof(MongoDbCollection))]
[Trait("Category", "Integration")]
public class MongoRepositoryTests
{
    private readonly MongoDbFixture _fixture;
    private readonly Fixture _autoFixture = new();

    public MongoRepositoryTests(MongoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AddAsync_ShouldInsertDocument()
    {
        // Arrange
        var collection = _fixture.GetFreshCollection<TestProduct>("products_add");
        var context = new TestContext();
        var repo = new TestProductRepository(context, collection);
        var product = _autoFixture.Create<TestProduct>();

        // Act
        using var session = await _fixture.Client.StartSessionAsync();
        await repo.AddAsync(session, product);

        // Assert
        var found = await repo.GetByIdAsync(product.Id!);
        found.Should().NotBeNull();
        found!.Name.Should().Be(product.Name);
        found.Price.Should().Be(product.Price);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ShouldReturnNull()
    {
        // Arrange
        var collection = _fixture.GetFreshCollection<TestProduct>("products_notfound");
        var context = new TestContext();
        var repo = new TestProductRepository(context, collection);
        var nonExistentId = _autoFixture.Create<string>();

        // Act
        var found = await repo.GetByIdAsync(nonExistentId);

        // Assert
        found.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_ShouldModifyDocument()
    {
        // Arrange
        var collection = _fixture.GetFreshCollection<TestProduct>("products_update");
        var context = new TestContext();
        var repo = new TestProductRepository(context, collection);
        var product = _autoFixture.Create<TestProduct>();
        var newPrice = _autoFixture.Create<decimal>();

        using var session = await _fixture.Client.StartSessionAsync();
        await repo.AddAsync(session, product);

        // Act
        product.Price = newPrice;
        await repo.UpdateAsync(session, product);

        // Assert
        var updated = await repo.GetByIdAsync(product.Id!);
        updated!.Price.Should().Be(newPrice);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveDocument()
    {
        // Arrange
        var collection = _fixture.GetFreshCollection<TestProduct>("products_delete");
        var context = new TestContext();
        var repo = new TestProductRepository(context, collection);
        var product = _autoFixture.Create<TestProduct>();

        using var session = await _fixture.Client.StartSessionAsync();
        await repo.AddAsync(session, product);

        // Act
        await repo.DeleteAsync(session, product.Id!);

        // Assert
        var deleted = await repo.GetByIdAsync(product.Id!);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllDocuments()
    {
        // Arrange
        var collection = _fixture.GetFreshCollection<TestProduct>("products_all");
        var context = new TestContext();
        var repo = new TestProductRepository(context, collection);
        var products = _autoFixture.CreateMany<TestProduct>(3).ToList();

        using var session = await _fixture.Client.StartSessionAsync();
        foreach (var product in products)
            await repo.AddAsync(session, product);

        // Act
        var all = await repo.GetAllAsync();

        // Assert
        all.Should().HaveCount(3);
    }

    #region Test Helpers

    private class TestProduct : MongoEntity<string>
    {
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    private class TestProductRepository : MongoRepository<TestProduct, string>
    {
        public TestProductRepository(IMongoDbContext context, IMongoCollection<TestProduct> collection)
            : base(context, collection)
        {
        }
    }

    private class TestContext : IMongoDbContext
    {
        private readonly List<Func<IClientSessionHandle, Task>> _commands = new();

        public int PendingCommands => _commands.Count;

        public void AddCommand(Func<IClientSessionHandle, Task> command) => _commands.Add(command);

        public async Task<int> SaveChangesAsync()
        {
            // Not used in tests - we use session-based operations
            return await Task.FromResult(_commands.Count);
        }
    }

    #endregion
}
