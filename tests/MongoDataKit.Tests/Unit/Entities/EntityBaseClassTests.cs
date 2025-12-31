using AutoFixture.Xunit2;
using FluentAssertions;
using MongoDataKit.Core.Interfaces;
using MongoDataKit.Persistence.Entities;
using Xunit;

namespace MongoDataKit.Tests.Unit.Entities;

[Trait("Category", "Unit")]
public class EntityBaseClassTests
{
    #region MongoEntity Tests

    [Theory, AutoData]
    public void MongoEntity_Id_ShouldBeSettable(string id)
    {
        // Arrange & Act
        var entity = new TestMongoEntity { Id = id };

        // Assert
        entity.Id.Should().Be(id);
    }

    [Theory, AutoData]
    public void MongoEntity_IEntity_Id_ShouldReturnEntityId(string id)
    {
        // Arrange
        var entity = new TestMongoEntity { Id = id };

        // Act
        var returnedId = ((IEntity)entity).Id;

        // Assert
        returnedId.Should().Be(id);
    }

    [Fact]
    public void MongoEntity_IEntity_Id_WhenNull_ShouldThrow()
    {
        // Arrange
        var entity = new TestMongoEntity { Id = null };

        // Act
        var act = () => ((IEntity)entity).Id;

        // Assert
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*Id not set*");
    }

    #endregion

    #region AuditableEntity Tests

    [Theory, AutoData]
    public void AuditableEntity_ShouldHaveAuditFields(
        string id, string createdBy, string modifiedBy)
    {
        // Arrange & Act
        var now = DateTime.UtcNow;
        var entity = new TestAuditableEntity
        {
            Id = id,
            CreatedAt = now,
            CreatedBy = createdBy,
            ModifiedAt = now.AddHours(1),
            ModifiedBy = modifiedBy
        };

        // Assert
        entity.Should().BeAssignableTo<IAuditable>();
        entity.CreatedBy.Should().Be(createdBy);
        entity.ModifiedBy.Should().Be(modifiedBy);
    }

    [Fact]
    public void AuditableEntity_ShouldInheritFromMongoEntity()
    {
        // Arrange & Act
        var entity = new TestAuditableEntity();

        // Assert
        entity.Should().BeAssignableTo<MongoEntity<string>>();
    }

    #endregion

    #region SoftDeleteEntity Tests

    [Theory, AutoData]
    public void SoftDeleteEntity_ShouldHaveSoftDeleteFields(string id, string deletedBy)
    {
        // Arrange & Act
        var entity = new TestSoftDeleteEntity
        {
            Id = id,
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow,
            DeletedBy = deletedBy
        };

        // Assert
        entity.Should().BeAssignableTo<ISoftDeletable>();
        entity.IsDeleted.Should().BeTrue();
        entity.DeletedBy.Should().Be(deletedBy);
    }

    [Fact]
    public void SoftDeleteEntity_DefaultIsDeleted_ShouldBeFalse()
    {
        // Arrange & Act
        var entity = new TestSoftDeleteEntity();

        // Assert
        entity.IsDeleted.Should().BeFalse();
        entity.DeletedAt.Should().BeNull();
        entity.DeletedBy.Should().BeNull();
    }

    [Fact]
    public void SoftDeleteEntity_ShouldInheritFromAuditableEntity()
    {
        // Arrange & Act
        var entity = new TestSoftDeleteEntity();

        // Assert
        entity.Should().BeAssignableTo<AuditableEntity<string>>();
        entity.Should().BeAssignableTo<IAuditable>();
    }

    #endregion

    #region VersionedEntity Tests

    [Theory, AutoData]
    public void VersionedEntity_ShouldHaveVersionField(string id, int version)
    {
        // Arrange - ensure positive version
        var positiveVersion = Math.Abs(version);

        // Act
        var entity = new TestVersionedEntity
        {
            Id = id,
            Version = positiveVersion
        };

        // Assert
        entity.Should().BeAssignableTo<IVersioned>();
        entity.Version.Should().Be(positiveVersion);
    }

    [Fact]
    public void VersionedEntity_DefaultVersion_ShouldBeZero()
    {
        // Arrange & Act
        var entity = new TestVersionedEntity();

        // Assert
        entity.Version.Should().Be(0);
    }

    [Fact]
    public void VersionedEntity_ShouldInheritFromAuditableEntity()
    {
        // Arrange & Act
        var entity = new TestVersionedEntity();

        // Assert
        entity.Should().BeAssignableTo<AuditableEntity<string>>();
        entity.Should().BeAssignableTo<IAuditable>();
    }

    #endregion

    #region FullFeaturedEntity Tests

    [Fact]
    public void FullFeaturedEntity_ShouldImplementAllInterfaces()
    {
        // Arrange & Act
        var entity = new TestFullFeaturedEntity();

        // Assert
        entity.Should().BeAssignableTo<IAuditable>();
        entity.Should().BeAssignableTo<ISoftDeletable>();
        entity.Should().BeAssignableTo<IVersioned>();
        entity.Should().BeAssignableTo<IEntity>();
    }

    [Theory, AutoData]
    public void FullFeaturedEntity_ShouldHaveAllFields(
        string id, string creator, string modifier, int version)
    {
        // Arrange & Act
        var entity = new TestFullFeaturedEntity
        {
            Id = id,
            // Audit
            CreatedAt = DateTime.UtcNow,
            CreatedBy = creator,
            ModifiedAt = DateTime.UtcNow,
            ModifiedBy = modifier,
            // Soft delete
            IsDeleted = false,
            DeletedAt = null,
            DeletedBy = null,
            // Version
            Version = Math.Abs(version)
        };

        // Assert
        entity.CreatedBy.Should().Be(creator);
        entity.ModifiedBy.Should().Be(modifier);
        entity.IsDeleted.Should().BeFalse();
        entity.Version.Should().Be(Math.Abs(version));
    }

    #endregion

    #region Test Entity Classes

    private class TestMongoEntity : MongoEntity<string> { }
    private class TestAuditableEntity : AuditableEntity<string> { }
    private class TestSoftDeleteEntity : SoftDeleteEntity<string> { }
    private class TestVersionedEntity : VersionedEntity<string> { }
    private class TestFullFeaturedEntity : FullFeaturedEntity<string> { }

    #endregion
}
