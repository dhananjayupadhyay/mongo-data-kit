using MongoDB.Bson;
using MongoDB.Driver;
using MongoDataKit.Core.Interfaces;

namespace MongoDataKit.Accessors.Search;

/// <summary>
/// Options for text search operations.
/// </summary>
public class TextSearchOptions
{
    /// <summary>
    /// The language for text search. Default is "english".
    /// </summary>
    public string Language { get; set; } = "english";

    /// <summary>
    /// Whether the search is case sensitive. Default is false.
    /// </summary>
    public bool CaseSensitive { get; set; } = false;

    /// <summary>
    /// Whether diacritic marks are significant. Default is false.
    /// </summary>
    public bool DiacriticSensitive { get; set; } = false;

    /// <summary>
    /// Maximum number of results to return. Default is 100.
    /// </summary>
    public int Limit { get; set; } = 100;

    /// <summary>
    /// Number of results to skip. Default is 0.
    /// </summary>
    public int Skip { get; set; } = 0;

    /// <summary>
    /// Whether to include the text score in results. Default is true.
    /// </summary>
    public bool IncludeScore { get; set; } = true;

    /// <summary>
    /// Sort by text score (relevance). Default is true.
    /// </summary>
    public bool SortByScore { get; set; } = true;
}

/// <summary>
/// Result of a text search operation with relevance score.
/// </summary>
/// <typeparam name="T">The document type.</typeparam>
public class TextSearchResult<T>
{
    /// <summary>
    /// The matching document.
    /// </summary>
    public T Document { get; set; } = default!;

    /// <summary>
    /// The relevance score (higher is more relevant).
    /// </summary>
    public double Score { get; set; }
}

/// <summary>
/// Extension methods for MongoDB text search operations.
/// </summary>
public static class TextSearchExtensions
{
    /// <summary>
    /// Performs a text search on the collection.
    /// Requires a text index on the collection.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="collection">The MongoDB collection.</param>
    /// <param name="searchText">The text to search for.</param>
    /// <param name="options">Optional search options.</param>
    /// <returns>List of matching documents ordered by relevance.</returns>
    public static async Task<IReadOnlyList<T>> TextSearchAsync<T>(
        this IMongoCollection<T> collection,
        string searchText,
        TextSearchOptions? options = null) where T : IEntity
    {
        options ??= new TextSearchOptions();

        var filter = Builders<T>.Filter.Text(
            searchText,
            new TextSearchOptions
            {
                Language = options.Language,
                CaseSensitive = options.CaseSensitive,
                DiacriticSensitive = options.DiacriticSensitive
            }.ToMongoOptions());

        var findFluent = collection.Find(filter);

        if (options.SortByScore)
        {
            findFluent = findFluent.Sort(
                Builders<T>.Sort.MetaTextScore("score"));
        }

        if (options.Skip > 0)
            findFluent = findFluent.Skip(options.Skip);

        if (options.Limit > 0)
            findFluent = findFluent.Limit(options.Limit);

        return await findFluent.ToListAsync();
    }

    /// <summary>
    /// Performs a text search and returns results with relevance scores.
    /// Requires a text index on the collection.
    /// </summary>
    public static async Task<IReadOnlyList<TextSearchResult<T>>> TextSearchWithScoreAsync<T>(
        this IMongoCollection<T> collection,
        string searchText,
        TextSearchOptions? options = null) where T : IEntity
    {
        options ??= new TextSearchOptions();

        var filter = Builders<T>.Filter.Text(searchText);

        var pipeline = collection.Aggregate()
            .Match(filter)
            .AppendStage<BsonDocument>(new BsonDocument
            {
                { "$addFields", new BsonDocument("score", new BsonDocument("$meta", "textScore")) }
            })
            .Sort(new BsonDocument("score", new BsonDocument("$meta", "textScore")));

        if (options.Skip > 0)
            pipeline = pipeline.Skip(options.Skip);

        if (options.Limit > 0)
            pipeline = pipeline.Limit(options.Limit);

        var results = await pipeline.ToListAsync();

        return results.Select(doc =>
        {
            var score = doc.Contains("score") ? doc["score"].AsDouble : 0;
            doc.Remove("score");

            return new TextSearchResult<T>
            {
                Document = BsonSerializer.Deserialize<T>(doc),
                Score = score
            };
        }).ToList();
    }

    /// <summary>
    /// Searches for documents where the specified field contains the search text.
    /// Uses regex for partial matching (slower than text index but more flexible).
    /// </summary>
    public static async Task<IReadOnlyList<T>> SearchFieldAsync<T>(
        this IMongoCollection<T> collection,
        string fieldName,
        string searchText,
        bool caseInsensitive = true,
        int limit = 100)
    {
        var regexOptions = caseInsensitive ? "i" : "";
        var filter = Builders<T>.Filter.Regex(fieldName, new BsonRegularExpression(searchText, regexOptions));

        return await collection.Find(filter).Limit(limit).ToListAsync();
    }

    /// <summary>
    /// Counts documents matching the text search.
    /// </summary>
    public static async Task<long> TextSearchCountAsync<T>(
        this IMongoCollection<T> collection,
        string searchText) where T : IEntity
    {
        var filter = Builders<T>.Filter.Text(searchText);
        return await collection.CountDocumentsAsync(filter);
    }

    private static MongoDB.Driver.TextSearchOptions ToMongoOptions(this TextSearchOptions options)
    {
        return new MongoDB.Driver.TextSearchOptions
        {
            Language = options.Language,
            CaseSensitive = options.CaseSensitive,
            DiacriticSensitive = options.DiacriticSensitive
        };
    }
}

/// <summary>
/// BSON serializer helper for text search.
/// </summary>
internal static class BsonSerializer
{
    public static T Deserialize<T>(BsonDocument doc)
    {
        return MongoDB.Bson.Serialization.BsonSerializer.Deserialize<T>(doc);
    }
}
