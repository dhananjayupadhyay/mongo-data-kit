using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDataKit.Core.Configuration;

namespace MongoDataKit.Initializer;

public interface IDatabaseInitializer
{
    Task InitializeAsync();
}

public sealed class DatabaseInitializer : IDatabaseInitializer
{
    private readonly ILogger<DatabaseInitializer> _logger;
    private readonly MongoSettings _settings;
    private readonly IMongoClient _client;

    public DatabaseInitializer(
        ILogger<DatabaseInitializer> logger,
        IOptions<MongoSettings> settings,
        IMongoClient client)
    {
        _logger = logger;
        _settings = settings.Value;
        _client = client;
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing MongoDB database");
        try
        {
            var database = _client.GetDatabase(_settings.DatabaseName);
            foreach (var (name, config) in _settings.Collections)
            {
                var collection = await EnsureCollectionAsync(database, name, config);
                if (collection != null)
                {
                    await CreateIndexesAsync(collection, name, config);
                    await ApplySchemaValidationAsync(database, name, config);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize MongoDB");
            throw;
        }
    }

    private async Task<IMongoCollection<object>?> EnsureCollectionAsync(
        IMongoDatabase database, string collectionName, CollectionSettings config)
    {
        using var cursor = await database.ListCollectionNamesAsync();
        var existing = await cursor.ToListAsync();

        if (!existing.Contains(collectionName, StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Creating collection {Collection}", collectionName);

            // Create with validation if specified
            if (config.Validation?.JsonSchema != null)
            {
                var options = BuildCreateCollectionOptions(config.Validation);
                await database.CreateCollectionAsync(collectionName, options);
            }
            else
            {
                await database.CreateCollectionAsync(collectionName);
            }
        }
        return database.GetCollection<object>(collectionName);
    }

    private async Task CreateIndexesAsync(
        IMongoCollection<object> collection,
        string collectionName,
        CollectionSettings config)
    {
        var existingIndexes = await GetExistingIndexNamesAsync(collectionName);
        var newIndexes = config.Indexes
            .Where(i => !existingIndexes.Contains(i.Key))
            .ToList();

        foreach (var (indexName, indexDef) in newIndexes)
        {
            if (!ValidateIndex(indexDef, indexName)) continue;

            var (options, keys) = BuildIndexModel(indexName, indexDef);
            var model = new CreateIndexModel<object>(keys, options);

            _logger.LogInformation("Creating index {Index} on {Collection}",
                indexName, collectionName);
            await collection.Indexes.CreateOneAsync(model);
        }
    }

    private async Task ApplySchemaValidationAsync(
        IMongoDatabase database,
        string collectionName,
        CollectionSettings config)
    {
        if (config.Validation?.JsonSchema == null) return;

        try
        {
            var validationLevel = config.Validation.Level switch
            {
                ValidationLevel.Off => "off",
                ValidationLevel.Moderate => "moderate",
                _ => "strict"
            };

            var validationAction = config.Validation.Action switch
            {
                ValidationAction.Warn => "warn",
                _ => "error"
            };

            var command = new BsonDocument
            {
                { "collMod", collectionName },
                { "validator", new BsonDocument("$jsonSchema",
                    BsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(config.Validation.JsonSchema))) },
                { "validationLevel", validationLevel },
                { "validationAction", validationAction }
            };

            await database.RunCommandAsync<BsonDocument>(command);
            _logger.LogInformation("Applied schema validation to {Collection}", collectionName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to apply schema validation to {Collection}", collectionName);
        }
    }

    private static CreateCollectionOptions<BsonDocument> BuildCreateCollectionOptions(SchemaValidationSettings validation)
    {
        var options = new CreateCollectionOptions<BsonDocument>();

        if (validation.JsonSchema != null)
        {
            var schema = new BsonDocument("$jsonSchema",
                BsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(validation.JsonSchema)));
            options.Validator = new FilterDefinitionBuilder<BsonDocument>().Eq("$jsonSchema", schema["$jsonSchema"]);
        }

        options.ValidationLevel = validation.Level switch
        {
            ValidationLevel.Off => DocumentValidationLevel.Off,
            ValidationLevel.Moderate => DocumentValidationLevel.Moderate,
            _ => DocumentValidationLevel.Strict
        };

        options.ValidationAction = validation.Action switch
        {
            ValidationAction.Warn => DocumentValidationAction.Warn,
            _ => DocumentValidationAction.Error
        };

        return options;
    }

    private bool ValidateIndex(IndexSettings index, string name)
    {
        if (!index.Ttl.HasValue) return true;

        if (index.Fields.Count != 1 || index.Ttl <= TimeSpan.Zero)
        {
            _logger.LogError("Invalid TTL index: {Index}", name);
            return false;
        }
        return true;
    }

    private async Task<HashSet<string>> GetExistingIndexNamesAsync(string collectionName)
    {
        var db = _client.GetDatabase(_settings.DatabaseName);
        var collection = db.GetCollection<object>(collectionName);
        using var cursor = await collection.Indexes.ListAsync();
        var docs = await cursor.ToListAsync();
        return docs.Select(d => d["name"].AsString).ToHashSet();
    }

    private (CreateIndexOptions, IndexKeysDefinition<object>) BuildIndexModel(
        string name, IndexSettings index)
    {
        var builder = new IndexKeysDefinitionBuilder<object>();

        // Check if this is a text index
        var isTextIndex = index.Fields.Any(f => f.IndexKind == Core.Configuration.IndexKind.Text);

        IndexKeysDefinition<object> keys;
        if (isTextIndex)
        {
            // Text index
            var textFields = index.Fields
                .Where(f => f.IndexKind == Core.Configuration.IndexKind.Text)
                .Select(f => builder.Text(f.PropertyName));
            keys = builder.Combine(textFields);
        }
        else
        {
            // Regular index
            var regularFields = index.Fields.Select(f => f.IndexKind switch
            {
                Core.Configuration.IndexKind.Geo2dSphere => builder.Geo2DSphere(f.PropertyName),
                _ => f.SortDirection == Core.Configuration.SortDirection.Ascending
                    ? builder.Ascending(f.PropertyName)
                    : builder.Descending(f.PropertyName)
            });
            keys = builder.Combine(regularFields);
        }

        var options = new CreateIndexOptions
        {
            Name = name,
            Background = true,
            Unique = index.Unique,
            ExpireAfter = index.Ttl,
            Collation = index.CaseInsensitive
                ? new Collation("en", strength: CollationStrength.Secondary)
                : null
        };

        // Text index specific options
        if (isTextIndex)
        {
            if (!string.IsNullOrEmpty(index.TextLanguage))
                options.DefaultLanguage = index.TextLanguage;

            if (index.TextWeights != null && index.TextWeights.Count > 0)
            {
                var weights = new BsonDocument();
                foreach (var (field, weight) in index.TextWeights)
                    weights[field] = weight;
                options.Weights = weights;
            }
        }

        return (options, keys);
    }
}
