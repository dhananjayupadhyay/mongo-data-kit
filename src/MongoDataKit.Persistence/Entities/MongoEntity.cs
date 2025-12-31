using MongoDataKit.Abstractions;
using MongoDataKit.Core.Interfaces;

namespace MongoDataKit.Persistence.Entities;

/// <summary>
/// Base entity for MongoDB documents.
/// </summary>
public abstract class MongoEntity<TId> : Entity<TId>, IEntity
{
    object IEntity.Id => Id ?? throw new InvalidOperationException("Id not set");
}

/// <summary>
/// Entity with audit trail support (created/modified tracking).
/// </summary>
public abstract class AuditableEntity<TId> : MongoEntity<TId>, IAuditable
{
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }
}

/// <summary>
/// Entity with soft delete support.
/// Documents are marked as deleted rather than permanently removed.
/// </summary>
public abstract class SoftDeleteEntity<TId> : AuditableEntity<TId>, ISoftDeletable
{
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}

/// <summary>
/// Entity with optimistic concurrency support via version tracking.
/// </summary>
public abstract class VersionedEntity<TId> : AuditableEntity<TId>, IVersioned
{
    public int Version { get; set; }
}

/// <summary>
/// Full-featured entity with audit trail, soft delete, and optimistic concurrency.
/// Use this as the base class when you need all features.
/// </summary>
public abstract class FullFeaturedEntity<TId> : MongoEntity<TId>, IAuditable, ISoftDeletable, IVersioned
{
    // Audit trail
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    // Soft delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }

    // Optimistic concurrency
    public int Version { get; set; }
}
