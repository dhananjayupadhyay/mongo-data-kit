using AutoFixture;
using AutoFixture.Xunit2;
using FluentAssertions;
using MongoDataKit.Core.Configuration;
using Xunit;

namespace MongoDataKit.Tests.Unit.Configuration;

[Trait("Category", "Unit")]
public class MongoSettingsTests
{
    private readonly Fixture _fixture = new();

    [Fact]
    public void MongoSettings_DefaultValues_ShouldBeEmpty()
    {
        // Arrange & Act
        var settings = new MongoSettings();

        // Assert
        settings.ConnectionString.Should().BeEmpty();
        settings.DatabaseName.Should().BeEmpty();
        settings.Collections.Should().BeEmpty();
        settings.SupportsTransactions.Should().BeFalse();
    }

    [Theory, AutoData]
    public void MongoSettings_WithValues_ShouldStoreCorrectly(
        string connectionString, string databaseName)
    {
        // Arrange & Act
        var settings = new MongoSettings
        {
            ConnectionString = connectionString,
            DatabaseName = databaseName,
            SupportsTransactions = true
        };

        // Assert
        settings.ConnectionString.Should().Be(connectionString);
        settings.DatabaseName.Should().Be(databaseName);
        settings.SupportsTransactions.Should().BeTrue();
    }

    [Theory, AutoData]
    public void CollectionSettings_WithIndexes_ShouldStoreCorrectly(
        string indexName, string propertyName)
    {
        // Arrange & Act
        var collection = new CollectionSettings
        {
            Indexes = new Dictionary<string, IndexSettings>
            {
                [indexName] = new IndexSettings
                {
                    Fields = new List<IndexField>
                    {
                        new() { PropertyName = propertyName, SortDirection = SortDirection.Ascending }
                    },
                    Unique = true
                }
            }
        };

        // Assert
        collection.Indexes.Should().ContainKey(indexName);
        collection.Indexes[indexName].Unique.Should().BeTrue();
        collection.Indexes[indexName].Fields.Should().HaveCount(1);
        collection.Indexes[indexName].Fields[0].PropertyName.Should().Be(propertyName);
    }

    [Theory, AutoData]
    public void IndexSettings_WithTtl_ShouldStoreCorrectly(TimeSpan ttl)
    {
        // Arrange - ensure positive TTL
        var positiveTtl = TimeSpan.FromHours(Math.Abs(ttl.TotalHours) + 1);

        // Act
        var index = new IndexSettings
        {
            Fields = _fixture.CreateMany<IndexField>(1).ToList(),
            Ttl = positiveTtl
        };

        // Assert
        index.Ttl.Should().Be(positiveTtl);
    }

    [Theory, AutoData]
    public void IndexField_WithGeoSphere_ShouldStoreCorrectly(string propertyName)
    {
        // Arrange & Act
        var field = new IndexField
        {
            PropertyName = propertyName,
            IndexKind = IndexKind.Geo2dSphere
        };

        // Assert
        field.IndexKind.Should().Be(IndexKind.Geo2dSphere);
        field.PropertyName.Should().Be(propertyName);
    }

    [Theory, AutoData]
    public void IndexField_WithText_ShouldStoreCorrectly(string propertyName)
    {
        // Arrange & Act
        var field = new IndexField
        {
            PropertyName = propertyName,
            IndexKind = IndexKind.Text
        };

        // Assert
        field.IndexKind.Should().Be(IndexKind.Text);
    }

    [Fact]
    public void SchemaValidationSettings_DefaultValues_ShouldBeStrictError()
    {
        // Arrange & Act
        var validation = new SchemaValidationSettings();

        // Assert
        validation.Level.Should().Be(ValidationLevel.Strict);
        validation.Action.Should().Be(ValidationAction.Error);
        validation.JsonSchema.Should().BeNull();
    }

    [Theory, AutoData]
    public void SchemaValidationSettings_WithWarnAction_ShouldStoreCorrectly(
        string schemaKey, string schemaValue)
    {
        // Arrange & Act
        var validation = new SchemaValidationSettings
        {
            Level = ValidationLevel.Moderate,
            Action = ValidationAction.Warn,
            JsonSchema = new Dictionary<string, object>
            {
                [schemaKey] = schemaValue
            }
        };

        // Assert
        validation.Level.Should().Be(ValidationLevel.Moderate);
        validation.Action.Should().Be(ValidationAction.Warn);
        validation.JsonSchema.Should().NotBeNull();
        validation.JsonSchema.Should().ContainKey(schemaKey);
    }
}
