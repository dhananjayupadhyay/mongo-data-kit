namespace MongoDataKit.Core.Interfaces;

/// <summary>
/// Interface for entities that support soft deletion.
/// Documents are marked as deleted rather than being permanently removed.
/// </summary>
public interface ISoftDeletable
{
    /// <summary>
    /// Indicates whether the document has been soft-deleted.
    /// </summary>
    bool IsDeleted { get; set; }

    /// <summary>
    /// The UTC timestamp when the document was soft-deleted.
    /// </summary>
    DateTime? DeletedAt { get; set; }

    /// <summary>
    /// The identifier of the user who soft-deleted the document.
    /// </summary>
    string? DeletedBy { get; set; }
}

/// <summary>
/// Interface for entities that track creation and modification audit information.
/// </summary>
public interface IAuditable
{
    /// <summary>
    /// The UTC timestamp when the document was created.
    /// </summary>
    DateTime CreatedAt { get; set; }

    /// <summary>
    /// The identifier of the user who created the document.
    /// </summary>
    string? CreatedBy { get; set; }

    /// <summary>
    /// The UTC timestamp when the document was last modified.
    /// </summary>
    DateTime? ModifiedAt { get; set; }

    /// <summary>
    /// The identifier of the user who last modified the document.
    /// </summary>
    string? ModifiedBy { get; set; }
}

/// <summary>
/// Interface for entities that support optimistic concurrency control.
/// Updates will fail if the version has been modified by another process.
/// </summary>
public interface IVersioned
{
    /// <summary>
    /// The version number, incremented on each update.
    /// Used for optimistic concurrency control.
    /// </summary>
    int Version { get; set; }
}
