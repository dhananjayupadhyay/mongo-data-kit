using MongoDB.Driver;

namespace MongoDataKit.Accessors.Extensions;

public static class AggregationExtensions
{
    public static Task<(int Count, IReadOnlyList<T> Data)> AggregatePagedAsync<T>(
        this IMongoCollection<T> collection,
        FilterDefinition<T> filter,
        SortDefinition<T>? sort,
        int skip,
        int limit) => AggregatePagedAsync(collection, null, filter, sort, skip, limit);

    public static async Task<(int Count, IReadOnlyList<T> Data)> AggregatePagedAsync<T>(
        this IMongoCollection<T> collection,
        IClientSessionHandle? session,
        FilterDefinition<T> filter,
        SortDefinition<T>? sort,
        int skip,
        int limit)
    {
        var countFacet = AggregateFacet.Create("count",
            PipelineDefinition<T, AggregateCountResult>.Create(new[]
            {
                PipelineStageDefinitionBuilder.Count<T>()
            }));

        var stages = new List<IPipelineStageDefinition>();
        if (sort != null) stages.Add(PipelineStageDefinitionBuilder.Sort(sort));
        if (skip > 0) stages.Add(PipelineStageDefinitionBuilder.Skip<T>(skip));
        if (limit > 0) stages.Add(PipelineStageDefinitionBuilder.Limit<T>(limit));

        var dataFacet = AggregateFacet.Create("data",
            PipelineDefinition<T, T>.Create(stages));

        var pipeline = session == null
            ? collection.Aggregate()
            : collection.Aggregate(session);

        var result = await pipeline
            .Match(filter)
            .Facet(countFacet, dataFacet)
            .ToListAsync();

        var facets = result.First().Facets;
        var count = facets.First(f => f.Name == "count")
            .Output<AggregateCountResult>()
            .FirstOrDefault()?.Count ?? 0;
        var data = facets.First(f => f.Name == "data").Output<T>();

        return ((int)count, data);
    }
}
