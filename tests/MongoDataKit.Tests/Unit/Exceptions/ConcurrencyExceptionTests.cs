using AutoFixture.Xunit2;
using FluentAssertions;
using MongoDataKit.Core.Exceptions;
using Xunit;

namespace MongoDataKit.Tests.Unit.Exceptions;

[Trait("Category", "Unit")]
public class ConcurrencyExceptionTests
{
    [Theory, AutoData]
    public void ConcurrencyException_WithMessage_ShouldStoreMessage(string message)
    {
        // Arrange & Act
        var ex = new ConcurrencyException(message);

        // Assert
        ex.Message.Should().Be(message);
        ex.EntityId.Should().BeNull();
        ex.EntityType.Should().BeNull();
    }

    [Theory, AutoData]
    public void ConcurrencyException_WithInnerException_ShouldStoreInner(
        string outerMessage, string innerMessage)
    {
        // Arrange
        var inner = new InvalidOperationException(innerMessage);

        // Act
        var ex = new ConcurrencyException(outerMessage, inner);

        // Assert
        ex.Message.Should().Be(outerMessage);
        ex.InnerException.Should().BeSameAs(inner);
    }

    [Theory, AutoData]
    public void ConcurrencyException_WithEntityDetails_ShouldStoreDetails(
        string entityId, int expectedVersion)
    {
        // Arrange & Act
        var ex = new ConcurrencyException(entityId, typeof(TestEntity), expectedVersion);

        // Assert
        ex.EntityId.Should().Be(entityId);
        ex.EntityType.Should().Be(typeof(TestEntity));
        ex.ExpectedVersion.Should().Be(expectedVersion);
        ex.Message.Should().Contain(entityId);
        ex.Message.Should().Contain("TestEntity");
        ex.Message.Should().Contain(expectedVersion.ToString());
    }

    [Theory, AutoData]
    public void ConcurrencyException_MessageFormat_ShouldBeDescriptive(string entityId)
    {
        // Arrange & Act
        var ex = new ConcurrencyException(entityId, typeof(TestEntity), 3);

        // Assert
        ex.Message.Should().Contain("Concurrency conflict");
        ex.Message.Should().Contain("modified by another process");
    }

    private class TestEntity { }
}
