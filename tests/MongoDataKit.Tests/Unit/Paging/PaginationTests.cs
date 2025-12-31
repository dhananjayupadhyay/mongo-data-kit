using AutoFixture;
using AutoFixture.Xunit2;
using FluentAssertions;
using MongoDataKit.Core.Paging;
using Xunit;

namespace MongoDataKit.Tests.Unit.Paging;

[Trait("Category", "Unit")]
public class PaginationTests
{
    private readonly Fixture _fixture = new();

    [Theory, AutoData]
    public void PagedResult_ShouldStoreValues(List<string> items, int totalCount, int pageSize, int skip)
    {
        // Arrange & Act
        var result = new PagedResult<string>(items)
        {
            TotalCount = totalCount,
            PageSize = pageSize,
            Skip = skip
        };

        // Assert
        result.Items.Should().BeEquivalentTo(items);
        result.TotalCount.Should().Be(totalCount);
        result.PageSize.Should().Be(pageSize);
        result.Skip.Should().Be(skip);
    }

    [Theory, AutoData]
    public void PagedResult_Items_ShouldBeReadOnly(List<string> items)
    {
        // Arrange & Act
        var result = new PagedResult<string>(items);

        // Assert
        result.Items.Should().BeAssignableTo<IReadOnlyList<string>>();
        result.Items.Should().BeEquivalentTo(items);
    }

    [Fact]
    public void PagedResult_EmptyItems_ShouldWork()
    {
        // Arrange & Act
        var result = new PagedResult<string>(new List<string>())
        {
            TotalCount = 0,
            PageSize = 10,
            Skip = 0
        };

        // Assert
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Theory]
    [InlineData(100, 10, 0)]   // First page
    [InlineData(100, 10, 90)]  // Last page
    [InlineData(15, 10, 10)]   // Partial last page
    public void PagedResult_WithVariousPaginationParams_ShouldWork(
        int totalCount, int pageSize, int skip)
    {
        // Arrange - use AutoFixture for the items
        var items = _fixture.CreateMany<int>().ToList();

        // Act
        var result = new PagedResult<int>(items)
        {
            TotalCount = totalCount,
            PageSize = pageSize,
            Skip = skip
        };

        // Assert
        result.TotalCount.Should().Be(totalCount);
        result.PageSize.Should().Be(pageSize);
        result.Skip.Should().Be(skip);
    }
}
