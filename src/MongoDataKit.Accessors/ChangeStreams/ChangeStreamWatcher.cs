using MongoDB.Bson;
using MongoDB.Driver;
using MongoDataKit.Core.Interfaces;

namespace MongoDataKit.Accessors.ChangeStreams;

/// <summary>
/// Represents a change event from MongoDB Change Streams.
/// </summary>
/// <typeparam name="TDocument">The document type being watched.</typeparam>
public class ChangeEvent<TDocument> where TDocument : IEntity
{
    /// <summary>
    /// The type of change operation.
    /// </summary>
    public ChangeOperationType OperationType { get; set; }

    /// <summary>
    /// The full document after the change (for insert/update/replace).
    /// </summary>
    public TDocument? FullDocument { get; set; }

    /// <summary>
    /// The document key (contains _id).
    /// </summary>
    public BsonDocument? DocumentKey { get; set; }

    /// <summary>
    /// The resume token for resuming the change stream.
    /// </summary>
    public BsonDocument? ResumeToken { get; set; }

    /// <summary>
    /// The timestamp of the change.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// The namespace (database.collection) where the change occurred.
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// Update description for update operations.
    /// </summary>
    public UpdateDescription? UpdateDescription { get; set; }
}

/// <summary>
/// Describes the fields that were updated.
/// </summary>
public class UpdateDescription
{
    /// <summary>
    /// Fields that were updated with their new values.
    /// </summary>
    public BsonDocument? UpdatedFields { get; set; }

    /// <summary>
    /// Fields that were removed.
    /// </summary>
    public IReadOnlyList<string>? RemovedFields { get; set; }
}

/// <summary>
/// Type of change operation in a change stream.
/// </summary>
public enum ChangeOperationType
{
    Insert,
    Update,
    Replace,
    Delete,
    Invalidate,
    Drop,
    DropDatabase,
    Rename,
    Other
}

/// <summary>
/// Options for configuring a change stream watcher.
/// </summary>
public class ChangeStreamOptions
{
    /// <summary>
    /// Whether to return the full document on update operations.
    /// Default is true for insert/replace, false for update.
    /// </summary>
    public FullDocumentOption FullDocument { get; set; } = FullDocumentOption.UpdateLookup;

    /// <summary>
    /// Resume token to resume watching from a specific point.
    /// </summary>
    public BsonDocument? ResumeAfter { get; set; }

    /// <summary>
    /// Start at a specific operation time.
    /// </summary>
    public BsonTimestamp? StartAtOperationTime { get; set; }

    /// <summary>
    /// Maximum time to wait for new changes before returning.
    /// </summary>
    public TimeSpan? MaxAwaitTime { get; set; }

    /// <summary>
    /// Batch size for the cursor.
    /// </summary>
    public int? BatchSize { get; set; }
}

/// <summary>
/// Full document return options.
/// </summary>
public enum FullDocumentOption
{
    /// <summary>
    /// Don't return the full document.
    /// </summary>
    Default,

    /// <summary>
    /// Return the full document for update operations by looking it up.
    /// </summary>
    UpdateLookup,

    /// <summary>
    /// Return the full document before the change (MongoDB 6.0+).
    /// </summary>
    WhenAvailable
}

/// <summary>
/// Interface for watching changes on a MongoDB collection.
/// </summary>
/// <typeparam name="TDocument">The document type to watch.</typeparam>
public interface IChangeStreamWatcher<TDocument> where TDocument : IEntity
{
    /// <summary>
    /// Watches for all changes on the collection.
    /// </summary>
    IAsyncEnumerable<ChangeEvent<TDocument>> WatchAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Watches for changes with custom options.
    /// </summary>
    IAsyncEnumerable<ChangeEvent<TDocument>> WatchAsync(
        ChangeStreamOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Watches for changes matching a filter pipeline.
    /// </summary>
    IAsyncEnumerable<ChangeEvent<TDocument>> WatchAsync(
        PipelineDefinition<ChangeStreamDocument<TDocument>, ChangeStreamDocument<TDocument>> pipeline,
        ChangeStreamOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Watches for changes on a MongoDB collection using Change Streams.
/// Requires a replica set or sharded cluster.
/// </summary>
public class ChangeStreamWatcher<TDocument> : IChangeStreamWatcher<TDocument>
    where TDocument : IEntity
{
    private readonly IMongoCollection<TDocument> _collection;

    public ChangeStreamWatcher(IMongoCollection<TDocument> collection)
    {
        _collection = collection;
    }

    public async IAsyncEnumerable<ChangeEvent<TDocument>> WatchAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var change in WatchAsync(new ChangeStreamOptions(), cancellationToken))
        {
            yield return change;
        }
    }

    public async IAsyncEnumerable<ChangeEvent<TDocument>> WatchAsync(
        ChangeStreamOptions options,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var mongoOptions = BuildOptions(options);

        using var cursor = await _collection.WatchAsync(mongoOptions, cancellationToken);

        while (await cursor.MoveNextAsync(cancellationToken))
        {
            foreach (var change in cursor.Current)
            {
                yield return MapChangeEvent(change);
            }
        }
    }

    public async IAsyncEnumerable<ChangeEvent<TDocument>> WatchAsync(
        PipelineDefinition<ChangeStreamDocument<TDocument>, ChangeStreamDocument<TDocument>> pipeline,
        ChangeStreamOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var mongoOptions = options != null ? BuildOptions(options) : new MongoDB.Driver.ChangeStreamOptions();

        using var cursor = await _collection.WatchAsync(pipeline, mongoOptions, cancellationToken);

        while (await cursor.MoveNextAsync(cancellationToken))
        {
            foreach (var change in cursor.Current)
            {
                yield return MapChangeEvent(change);
            }
        }
    }

    private static MongoDB.Driver.ChangeStreamOptions BuildOptions(ChangeStreamOptions options)
    {
        var mongoOptions = new MongoDB.Driver.ChangeStreamOptions
        {
            ResumeAfter = options.ResumeAfter,
            StartAtOperationTime = options.StartAtOperationTime,
            MaxAwaitTime = options.MaxAwaitTime,
            BatchSize = options.BatchSize
        };

        mongoOptions.FullDocument = options.FullDocument switch
        {
            FullDocumentOption.UpdateLookup => ChangeStreamFullDocumentOption.UpdateLookup,
            FullDocumentOption.WhenAvailable => ChangeStreamFullDocumentOption.WhenAvailable,
            _ => ChangeStreamFullDocumentOption.Default
        };

        return mongoOptions;
    }

    private static ChangeEvent<TDocument> MapChangeEvent(ChangeStreamDocument<TDocument> change)
    {
        return new ChangeEvent<TDocument>
        {
            OperationType = MapOperationType(change.OperationType),
            FullDocument = change.FullDocument,
            DocumentKey = change.DocumentKey,
            ResumeToken = change.ResumeToken,
            Timestamp = change.ClusterTime?.Timestamp != null
                ? DateTimeOffset.FromUnixTimeSeconds(change.ClusterTime.Timestamp).UtcDateTime
                : DateTime.UtcNow,
            Namespace = $"{change.DatabaseNamespace?.DatabaseName}.{change.CollectionNamespace?.CollectionName}",
            UpdateDescription = change.UpdateDescription != null
                ? new UpdateDescription
                {
                    UpdatedFields = change.UpdateDescription.UpdatedFields,
                    RemovedFields = change.UpdateDescription.RemovedFields?.ToList()
                }
                : null
        };
    }

    private static ChangeOperationType MapOperationType(MongoDB.Driver.ChangeStreamOperationType type)
    {
        return type switch
        {
            MongoDB.Driver.ChangeStreamOperationType.Insert => ChangeOperationType.Insert,
            MongoDB.Driver.ChangeStreamOperationType.Update => ChangeOperationType.Update,
            MongoDB.Driver.ChangeStreamOperationType.Replace => ChangeOperationType.Replace,
            MongoDB.Driver.ChangeStreamOperationType.Delete => ChangeOperationType.Delete,
            MongoDB.Driver.ChangeStreamOperationType.Invalidate => ChangeOperationType.Invalidate,
            MongoDB.Driver.ChangeStreamOperationType.Drop => ChangeOperationType.Drop,
            MongoDB.Driver.ChangeStreamOperationType.DropDatabase => ChangeOperationType.DropDatabase,
            MongoDB.Driver.ChangeStreamOperationType.Rename => ChangeOperationType.Rename,
            _ => ChangeOperationType.Other
        };
    }
}

/// <summary>
/// Extension methods for creating change stream watchers.
/// </summary>
public static class ChangeStreamExtensions
{
    /// <summary>
    /// Creates a change stream watcher for the collection.
    /// </summary>
    public static IChangeStreamWatcher<T> CreateWatcher<T>(this IMongoCollection<T> collection)
        where T : IEntity
    {
        return new ChangeStreamWatcher<T>(collection);
    }
}
