using MongoDB.Driver;
using MongoDataKit.Accessors.Filtering;
using MongoDataKit.Core.Interfaces;
using MongoDataKit.Core.Paging;

namespace MongoDataKit.Accessors.Extensions;

public static class CollectionExtensions
{
    public static Task<IPagedResult<T>> FindPagedAsync<T>(
        this IMongoCollection<T> collection,
        IQueryFilter<T> filter) where T : IEntity
        => FindPagedAsync(collection, null, filter);

    public static async Task<IPagedResult<T>> FindPagedAsync<T>(
        this IMongoCollection<T> collection,
        IClientSessionHandle? session,
        IQueryFilter<T> filter) where T : IEntity
    {
        var (count, data) = await collection.AggregatePagedAsync(
            session,
            filter.ToFilterDefinition(),
            filter.ToSortDefinition(),
            filter.Skip,
            filter.PageSize);

        return new PagedResult<T>(data)
        {
            TotalCount = count,
            PageSize = filter.PageSize,
            Skip = filter.Skip
        };
    }

    public static Task UpsertManyAsync<T>(
        this IMongoCollection<T> collection,
        IEnumerable<T> documents) where T : IEntity
        => UpsertManyAsync(collection, null, documents);

    public static Task UpsertManyAsync<T>(
        this IMongoCollection<T> collection,
        IClientSessionHandle? session,
        IEnumerable<T> documents) where T : IEntity
    {
        var operations = documents.Select(d =>
            new ReplaceOneModel<T>(
                Builders<T>.Filter.Eq(x => x.Id, d.Id), d)
            { IsUpsert = true });

        return session == null
            ? collection.BulkWriteAsync(operations)
            : collection.BulkWriteAsync(session, operations);
    }

    public static Task DeleteManyByIdsAsync<T>(
        this IMongoCollection<T> collection,
        IEnumerable<object> ids) where T : IEntity
        => DeleteManyByIdsAsync(collection, null, ids);

    public static Task DeleteManyByIdsAsync<T>(
        this IMongoCollection<T> collection,
        IClientSessionHandle? session,
        IEnumerable<object> ids) where T : IEntity
    {
        var filter = Builders<T>.Filter.In(x => x.Id, ids);
        return session == null
            ? collection.DeleteManyAsync(filter)
            : collection.DeleteManyAsync(session, filter);
    }
}
