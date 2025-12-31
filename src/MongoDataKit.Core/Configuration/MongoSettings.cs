using Destructurama.Attributed;

namespace MongoDataKit.Core.Configuration;

public class MongoSettings
{
    [LogMasked(Text = nameof(ConnectionString))]
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public Dictionary<string, CollectionSettings> Collections { get; set; } = new();
    public bool SupportsTransactions { get; set; }
}

public sealed class CollectionSettings
{
    /// <summary>
    /// Index definitions for the collection.
    /// </summary>
    public Dictionary<string, IndexSettings> Indexes { get; set; } = new();

    /// <summary>
    /// Schema validation settings for the collection.
    /// </summary>
    public SchemaValidationSettings? Validation { get; set; }
}

public class IndexSettings
{
    public List<IndexField> Fields { get; set; } = new();
    public bool Unique { get; set; }
    public bool CaseInsensitive { get; set; }
    public TimeSpan? Ttl { get; set; }

    /// <summary>
    /// For text indexes, the default language.
    /// </summary>
    public string? TextLanguage { get; set; }

    /// <summary>
    /// Weights for text index fields (field name -> weight).
    /// Higher weights give more relevance to that field.
    /// </summary>
    public Dictionary<string, int>? TextWeights { get; set; }
}

public class IndexField
{
    public string PropertyName { get; set; } = string.Empty;
    public SortDirection SortDirection { get; set; } = SortDirection.Ascending;
    public IndexKind IndexKind { get; set; } = IndexKind.Standard;
}

public enum SortDirection { Ascending, Descending }
public enum IndexKind { Standard, Geo2dSphere, Text }

/// <summary>
/// Schema validation settings for a MongoDB collection.
/// </summary>
public class SchemaValidationSettings
{
    /// <summary>
    /// JSON Schema definition for document validation.
    /// Use MongoDB's $jsonSchema format.
    /// </summary>
    public Dictionary<string, object>? JsonSchema { get; set; }

    /// <summary>
    /// Validation level: "off", "moderate", or "strict".
    /// - off: No validation
    /// - moderate: Validate inserts and updates to existing valid documents
    /// - strict: Validate all inserts and updates
    /// </summary>
    public ValidationLevel Level { get; set; } = ValidationLevel.Strict;

    /// <summary>
    /// Validation action: "error" or "warn".
    /// - error: Reject documents that fail validation
    /// - warn: Allow documents but log a warning
    /// </summary>
    public ValidationAction Action { get; set; } = ValidationAction.Error;
}

public enum ValidationLevel
{
    Off,
    Moderate,
    Strict
}

public enum ValidationAction
{
    Error,
    Warn
}
