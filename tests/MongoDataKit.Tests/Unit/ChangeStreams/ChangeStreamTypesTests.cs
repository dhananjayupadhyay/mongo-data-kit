using AutoFixture.Xunit2;
using FluentAssertions;
using MongoDataKit.Accessors.ChangeStreams;
using Xunit;

namespace MongoDataKit.Tests.Unit.ChangeStreams;

[Trait("Category", "Unit")]
public class ChangeStreamTypesTests
{
    [Theory, AutoData]
    public void ChangeEvent_ShouldStoreAllProperties(string name, string ns)
    {
        // Arrange & Act
        var evt = new ChangeEvent<TestDocument>
        {
            OperationType = ChangeOperationType.Insert,
            FullDocument = new TestDocument { Name = name },
            Timestamp = DateTime.UtcNow,
            Namespace = ns
        };

        // Assert
        evt.OperationType.Should().Be(ChangeOperationType.Insert);
        evt.FullDocument.Should().NotBeNull();
        evt.FullDocument!.Name.Should().Be(name);
        evt.Namespace.Should().Be(ns);
    }

    [Fact]
    public void ChangeStreamOptions_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var options = new ChangeStreamOptions();

        // Assert
        options.FullDocument.Should().Be(FullDocumentOption.UpdateLookup);
        options.ResumeAfter.Should().BeNull();
        options.StartAtOperationTime.Should().BeNull();
        options.MaxAwaitTime.Should().BeNull();
        options.BatchSize.Should().BeNull();
    }

    [Theory, AutoData]
    public void UpdateDescription_ShouldStoreFields(List<string> removedFields)
    {
        // Arrange & Act
        var update = new UpdateDescription
        {
            RemovedFields = removedFields
        };

        // Assert
        update.RemovedFields.Should().BeEquivalentTo(removedFields);
    }

    [Theory]
    [InlineData(ChangeOperationType.Insert)]
    [InlineData(ChangeOperationType.Update)]
    [InlineData(ChangeOperationType.Replace)]
    [InlineData(ChangeOperationType.Delete)]
    [InlineData(ChangeOperationType.Drop)]
    public void ChangeOperationType_AllValues_ShouldBeDefined(ChangeOperationType type)
    {
        // Assert
        Enum.IsDefined(typeof(ChangeOperationType), type).Should().BeTrue();
    }

    [Theory]
    [InlineData(FullDocumentOption.Default)]
    [InlineData(FullDocumentOption.UpdateLookup)]
    [InlineData(FullDocumentOption.WhenAvailable)]
    public void FullDocumentOption_AllValues_ShouldBeDefined(FullDocumentOption option)
    {
        // Assert
        Enum.IsDefined(typeof(FullDocumentOption), option).Should().BeTrue();
    }

    private class TestDocument : MongoDataKit.Core.Interfaces.IEntity
    {
        public object Id => "test-id";
        public string Name { get; set; } = string.Empty;
    }
}
