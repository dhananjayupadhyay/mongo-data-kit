using AutoFixture.Xunit2;
using FluentAssertions;
using MongoDataKit.Accessors.Search;
using Xunit;

namespace MongoDataKit.Tests.Unit.Search;

[Trait("Category", "Unit")]
public class TextSearchOptionsTests
{
    [Fact]
    public void TextSearchOptions_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var options = new TextSearchOptions();

        // Assert
        options.Language.Should().Be("english");
        options.CaseSensitive.Should().BeFalse();
        options.DiacriticSensitive.Should().BeFalse();
        options.Limit.Should().Be(100);
        options.Skip.Should().Be(0);
        options.IncludeScore.Should().BeTrue();
        options.SortByScore.Should().BeTrue();
    }

    [Theory, AutoData]
    public void TextSearchOptions_WithCustomValues_ShouldStoreCorrectly(
        string language, int limit, int skip)
    {
        // Arrange - ensure positive values
        var positiveLimit = Math.Abs(limit) + 1;
        var positiveSkip = Math.Abs(skip);

        // Act
        var options = new TextSearchOptions
        {
            Language = language,
            CaseSensitive = true,
            DiacriticSensitive = true,
            Limit = positiveLimit,
            Skip = positiveSkip,
            IncludeScore = false,
            SortByScore = false
        };

        // Assert
        options.Language.Should().Be(language);
        options.CaseSensitive.Should().BeTrue();
        options.DiacriticSensitive.Should().BeTrue();
        options.Limit.Should().Be(positiveLimit);
        options.Skip.Should().Be(positiveSkip);
        options.IncludeScore.Should().BeFalse();
        options.SortByScore.Should().BeFalse();
    }

    [Theory, AutoData]
    public void TextSearchResult_ShouldStoreDocumentAndScore(string title, double score)
    {
        // Arrange - ensure positive score
        var positiveScore = Math.Abs(score);

        // Act
        var result = new TextSearchResult<TestDocument>
        {
            Document = new TestDocument { Title = title },
            Score = positiveScore
        };

        // Assert
        result.Document.Title.Should().Be(title);
        result.Score.Should().Be(positiveScore);
    }

    private class TestDocument
    {
        public string Title { get; set; } = string.Empty;
    }
}
