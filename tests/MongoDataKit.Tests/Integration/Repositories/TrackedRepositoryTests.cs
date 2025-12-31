using AutoFixture;
using AutoFixture.Xunit2;
using FluentAssertions;
using MongoDB.Driver;
using MongoDataKit.Core.Exceptions;
using MongoDataKit.Core.Interfaces;
using MongoDataKit.Persistence;
using MongoDataKit.Persistence.Entities;
using MongoDataKit.Persistence.Repositories;
using MongoDataKit.Tests.Integration.Fixtures;
using Xunit;

namespace MongoDataKit.Tests.Integration.Repositories;

[Collection(nameof(MongoDbCollection))]
[Trait("Category", "Integration")]
public class TrackedRepositoryTests
{
    private readonly MongoDbFixture _fixture;
    private readonly Fixture _autoFixture = new();

    public TrackedRepositoryTests(MongoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Add_ShouldPopulateAuditFields()
    {
        // Arrange
        var userId = _autoFixture.Create<string>();
        var collection = _fixture.GetFreshCollection<TestOrder>("orders_audit");
        var auditContext = new TestAuditContext(userId);
        var context = new TestContext();
        var repo = new TestOrderRepository(context, collection, auditContext);
        var order = _autoFixture.Create<TestOrder>();

        // Act
        using var session = await _fixture.Client.StartSessionAsync();
        await repo.AddAsync(session, order);

        // Assert
        var found = await repo.GetByIdAsync(order.Id!);
        found.Should().NotBeNull();
        found!.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        found.CreatedBy.Should().Be(userId);
        found.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task SoftDelete_ShouldMarkAsDeleted()
    {
        // Arrange
        var userId = _autoFixture.Create<string>();
        var collection = _fixture.GetFreshCollection<TestOrder>("orders_softdelete");
        var auditContext = new TestAuditContext(userId);
        var context = new TestContext();
        var repo = new TestOrderRepository(context, collection, auditContext);
        var order = _autoFixture.Create<TestOrder>();

        using var session = await _fixture.Client.StartSessionAsync();
        await repo.AddAsync(session, order);

        // Act
        await repo.SoftDeleteAsync(session, order.Id!);

        // Assert - Should not be found via normal query
        var notFound = await repo.GetByIdAsync(order.Id!);
        notFound.Should().BeNull();

        // But should be found including deleted
        var found = await repo.GetByIdIncludingDeletedAsync(order.Id!);
        found.Should().NotBeNull();
        found!.IsDeleted.Should().BeTrue();
        found.DeletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        found.DeletedBy.Should().Be(userId);
    }

    [Fact]
    public async Task Restore_ShouldUnmarkAsDeleted()
    {
        // Arrange
        var userId = _autoFixture.Create<string>();
        var collection = _fixture.GetFreshCollection<TestOrder>("orders_restore");
        var auditContext = new TestAuditContext(userId);
        var context = new TestContext();
        var repo = new TestOrderRepository(context, collection, auditContext);
        var order = _autoFixture.Create<TestOrder>();

        using var session = await _fixture.Client.StartSessionAsync();
        await repo.AddAsync(session, order);
        await repo.SoftDeleteAsync(session, order.Id!);

        // Act
        await repo.RestoreAsync(session, order.Id!);

        // Assert
        var found = await repo.GetByIdAsync(order.Id!);
        found.Should().NotBeNull();
        found!.IsDeleted.Should().BeFalse();
        found.DeletedAt.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_ShouldExcludeSoftDeleted()
    {
        // Arrange
        var userId = _autoFixture.Create<string>();
        var collection = _fixture.GetFreshCollection<TestOrder>("orders_excluded");
        var auditContext = new TestAuditContext(userId);
        var context = new TestContext();
        var repo = new TestOrderRepository(context, collection, auditContext);
        var orders = _autoFixture.CreateMany<TestOrder>(3).ToList();

        using var session = await _fixture.Client.StartSessionAsync();
        foreach (var order in orders)
            await repo.AddAsync(session, order);

        // Soft delete the second one
        await repo.SoftDeleteAsync(session, orders[1].Id!);

        // Act
        var all = await repo.GetAllAsync();
        var allIncludingDeleted = await repo.GetAllIncludingDeletedAsync();

        // Assert
        all.Should().HaveCount(2);
        allIncludingDeleted.Should().HaveCount(3);
    }

    #region Test Helpers

    private class TestOrder : SoftDeleteEntity<string>
    {
        public decimal Total { get; set; }
    }

    private class TestOrderRepository : TrackedRepository<TestOrder, string>
    {
        public TestOrderRepository(
            IMongoDbContext context,
            IMongoCollection<TestOrder> collection,
            IAuditContext auditContext)
            : base(context, collection, auditContext)
        {
        }
    }

    private class TestAuditContext : IAuditContext
    {
        public string? CurrentUserId { get; }
        public TestAuditContext(string? userId) => CurrentUserId = userId;
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
