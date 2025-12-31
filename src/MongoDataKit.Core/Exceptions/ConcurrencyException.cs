namespace MongoDataKit.Core.Exceptions;

/// <summary>
/// Exception thrown when an optimistic concurrency conflict is detected.
/// This occurs when attempting to update a document that has been modified by another process.
/// </summary>
public class ConcurrencyException : Exception
{
    /// <summary>
    /// The ID of the entity that caused the conflict.
    /// </summary>
    public object? EntityId { get; }

    /// <summary>
    /// The type of the entity that caused the conflict.
    /// </summary>
    public Type? EntityType { get; }

    /// <summary>
    /// The expected version number.
    /// </summary>
    public int ExpectedVersion { get; }

    public ConcurrencyException(string message)
        : base(message)
    {
    }

    public ConcurrencyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public ConcurrencyException(object entityId, Type entityType, int expectedVersion)
        : base($"Concurrency conflict detected for {entityType.Name} with Id '{entityId}'. " +
               $"Expected version {expectedVersion} but document was modified by another process.")
    {
        EntityId = entityId;
        EntityType = entityType;
        ExpectedVersion = expectedVersion;
    }
}
