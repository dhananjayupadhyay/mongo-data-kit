namespace MongoDataKit.Core.Interfaces;

/// <summary>
/// Provides the current user context for audit trail functionality.
/// Implement this interface to provide the current user's identity.
/// </summary>
public interface IAuditContext
{
    /// <summary>
    /// Gets the identifier of the current user.
    /// Returns null if no user is authenticated.
    /// </summary>
    string? CurrentUserId { get; }
}

/// <summary>
/// Default implementation that returns no user (anonymous).
/// Replace with your own implementation to integrate with your authentication system.
/// </summary>
public class AnonymousAuditContext : IAuditContext
{
    public string? CurrentUserId => null;
}
