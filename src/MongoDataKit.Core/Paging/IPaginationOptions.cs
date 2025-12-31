namespace MongoDataKit.Core.Paging;

public interface IQueryConstraints
{
    TimeSpan MaxExecutionTime { get; set; }
    TimeSpan MaxAwaitTime { get; set; }
}

public interface IPaginationOptions : IQueryConstraints
{
    int PageSize { get; set; }
    int Skip { get; set; }
}

public interface IPagedResult<out T>
{
    int TotalCount { get; }
    int PageSize { get; }
    int Skip { get; }
    IReadOnlyList<T> Items { get; }
}

public sealed class PagedResult<T> : IPagedResult<T>
{
    public PagedResult(IReadOnlyList<T> items) => Items = items;
    public int TotalCount { get; set; }
    public int PageSize { get; set; }
    public int Skip { get; set; }
    public IReadOnlyList<T> Items { get; }
}
