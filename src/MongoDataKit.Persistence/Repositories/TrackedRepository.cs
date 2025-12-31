using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using MongoDataKit.Abstractions;
using MongoDataKit.Accessors.Extensions;
using MongoDataKit.Accessors.Filtering;
using MongoDataKit.Core.Exceptions;
using MongoDataKit.Core.Interfaces;
using MongoDataKit.Core.Paging;
using MongoDataKit.Persistence.Entities;

namespace MongoDataKit.Persistence.Repositories;

/// <summary>
/// Repository interface with soft delete support.
/// </summary>
public interface ITrackedRepository<TEntity, in TId> : IMongoRepository<TEntity, TId>
    where TEntity : MongoEntity<TId>, ISoftDeletable
{
    /// <summary>
    /// Soft deletes an entity by marking it as deleted.
    /// </summary>
    void SoftDelete(TEntity entity);

    /// <summary>
    /// Soft deletes an entity by ID.
    /// </summary>
    void SoftDelete(TId id);

    /// <summary>
    /// Soft deletes an entity within a session.
    /// </summary>
    Task SoftDeleteAsync(IClientSessionHandle session, TEntity entity);

    /// <summary>
    /// Soft deletes an entity by ID within a session.
    /// </summary>
    Task SoftDeleteAsync(IClientSessionHandle session, TId id);

    /// <summary>
    /// Restores a soft-deleted entity.
    /// </summary>
    void Restore(TId id);

    /// <summary>
    /// Restores a soft-deleted entity within a session.
    /// </summary>
    Task RestoreAsync(IClientSessionHandle session, TId id);

    /// <summary>
    /// Gets an entity by ID including soft-deleted entities.
    /// </summary>
    Task<TEntity?> GetByIdIncludingDeletedAsync(TId id);

    /// <summary>
    /// Gets all entities including soft-deleted ones.
    /// </summary>
    Task<IEnumerable<TEntity>> GetAllIncludingDeletedAsync();
}

/// <summary>
/// Repository with soft delete, audit trail, and optimistic concurrency support.
/// Tracks entity state including: when/who created, when/who modified, version, and deleted state.
/// </summary>
public abstract class TrackedRepository<TEntity, TId> : MongoRepository<TEntity, TId>, ITrackedRepository<TEntity, TId>
    where TEntity : MongoEntity<TId>, ISoftDeletable
{
    private readonly IAuditContext? _auditContext;

    protected TrackedRepository(
        IMongoDbContext context,
        IMongoCollection<TEntity> collection,
        IAuditContext? auditContext = null)
        : base(context, collection)
    {
        _auditContext = auditContext;
    }

    /// <summary>
    /// Gets the base filter that excludes soft-deleted documents.
    /// </summary>
    protected virtual FilterDefinition<TEntity> NotDeletedFilter
        => Builders<TEntity>.Filter.Eq(x => x.IsDeleted, false);

    #region Overridden Read Methods (Auto-filter soft-deleted)

    public new async Task<TEntity?> GetByIdAsync(TId id)
    {
        var filter = Builders<TEntity>.Filter.And(
            Builders<TEntity>.Filter.Eq("_id", id),
            NotDeletedFilter);

        using var cursor = await Collection.FindAsync(filter);
        return await cursor.SingleOrDefaultAsync();
    }

    public new async Task<TEntity?> GetByIdAsync(IClientSessionHandle session, TId id)
    {
        var filter = Builders<TEntity>.Filter.And(
            Builders<TEntity>.Filter.Eq("_id", id),
            NotDeletedFilter);

        using var cursor = await Collection.FindAsync(session, filter);
        return await cursor.SingleOrDefaultAsync();
    }

    public new async Task<IEnumerable<TEntity>> GetAllAsync()
    {
        using var cursor = await Collection.FindAsync(NotDeletedFilter);
        return await cursor.ToListAsync();
    }

    public new async Task<IEnumerable<TEntity>> GetAllAsync(IClientSessionHandle session)
    {
        using var cursor = await Collection.FindAsync(session, NotDeletedFilter);
        return await cursor.ToListAsync();
    }

    #endregion

    #region Soft Delete Methods

    public void SoftDelete(TEntity entity)
    {
        EnsureId(entity);
        SoftDelete(entity.Id!);
    }

    public void SoftDelete(TId id)
    {
        var update = Builders<TEntity>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, DateTime.UtcNow)
            .Set(x => x.DeletedBy, _auditContext?.CurrentUserId);

        Context.AddCommand(s => Collection.UpdateOneAsync(s,
            Builders<TEntity>.Filter.Eq("_id", id), update));
    }

    public Task SoftDeleteAsync(IClientSessionHandle session, TEntity entity)
    {
        EnsureId(entity);
        return SoftDeleteAsync(session, entity.Id!);
    }

    public Task SoftDeleteAsync(IClientSessionHandle session, TId id)
    {
        var update = Builders<TEntity>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, DateTime.UtcNow)
            .Set(x => x.DeletedBy, _auditContext?.CurrentUserId);

        return Collection.UpdateOneAsync(session,
            Builders<TEntity>.Filter.Eq("_id", id), update);
    }

    public void Restore(TId id)
    {
        var update = Builders<TEntity>.Update
            .Set(x => x.IsDeleted, false)
            .Set(x => x.DeletedAt, null)
            .Set(x => x.DeletedBy, null);

        Context.AddCommand(s => Collection.UpdateOneAsync(s,
            Builders<TEntity>.Filter.Eq("_id", id), update));
    }

    public Task RestoreAsync(IClientSessionHandle session, TId id)
    {
        var update = Builders<TEntity>.Update
            .Set(x => x.IsDeleted, false)
            .Set(x => x.DeletedAt, null)
            .Set(x => x.DeletedBy, null);

        return Collection.UpdateOneAsync(session,
            Builders<TEntity>.Filter.Eq("_id", id), update);
    }

    public async Task<TEntity?> GetByIdIncludingDeletedAsync(TId id)
    {
        using var cursor = await Collection.FindAsync(
            Builders<TEntity>.Filter.Eq("_id", id));
        return await cursor.SingleOrDefaultAsync();
    }

    public async Task<IEnumerable<TEntity>> GetAllIncludingDeletedAsync()
    {
        using var cursor = await Collection.FindAsync(
            Builders<TEntity>.Filter.Empty);
        return await cursor.ToListAsync();
    }

    #endregion

    #region Overridden Add Methods (Auto-populate audit fields)

    public new void Add(TEntity entity)
    {
        PopulateAuditFieldsOnCreate(entity);
        base.Add(entity);
    }

    public new Task AddAsync(IClientSessionHandle session, TEntity entity)
    {
        PopulateAuditFieldsOnCreate(entity);
        return base.AddAsync(session, entity);
    }

    public new void Add(IEnumerable<TEntity> entities)
    {
        foreach (var entity in entities)
            PopulateAuditFieldsOnCreate(entity);
        base.Add(entities);
    }

    public new Task AddAsync(IClientSessionHandle session, IEnumerable<TEntity> entities)
    {
        foreach (var entity in entities)
            PopulateAuditFieldsOnCreate(entity);
        return base.AddAsync(session, entities);
    }

    #endregion

    #region Overridden Update Methods (Auto-populate audit + version check)

    public new void Update(TEntity entity)
    {
        EnsureId(entity);
        PopulateAuditFieldsOnUpdate(entity);

        if (entity is IVersioned versioned)
        {
            var originalVersion = versioned.Version;
            versioned.Version++;

            Context.AddCommand(async s =>
            {
                var filter = Builders<TEntity>.Filter.And(
                    Builders<TEntity>.Filter.Eq("_id", entity.Id),
                    Builders<TEntity>.Filter.Eq(nameof(IVersioned.Version), originalVersion));

                var result = await Collection.ReplaceOneAsync(s, filter, entity);

                if (result.MatchedCount == 0)
                    throw new ConcurrencyException(entity.Id!, typeof(TEntity), originalVersion);
            });
        }
        else
        {
            base.Update(entity);
        }
    }

    public new async Task UpdateAsync(IClientSessionHandle session, TEntity entity)
    {
        EnsureId(entity);
        PopulateAuditFieldsOnUpdate(entity);

        if (entity is IVersioned versioned)
        {
            var originalVersion = versioned.Version;
            versioned.Version++;

            var filter = Builders<TEntity>.Filter.And(
                Builders<TEntity>.Filter.Eq("_id", entity.Id),
                Builders<TEntity>.Filter.Eq(nameof(IVersioned.Version), originalVersion));

            var result = await Collection.ReplaceOneAsync(session, filter, entity);

            if (result.MatchedCount == 0)
                throw new ConcurrencyException(entity.Id!, typeof(TEntity), originalVersion);
        }
        else
        {
            await base.UpdateAsync(session, entity);
        }
    }

    #endregion

    #region Helper Methods

    private void PopulateAuditFieldsOnCreate(TEntity entity)
    {
        if (entity is IAuditable auditable)
        {
            auditable.CreatedAt = DateTime.UtcNow;
            auditable.CreatedBy = _auditContext?.CurrentUserId;
        }

        if (entity is IVersioned versioned)
        {
            versioned.Version = 1;
        }

        if (entity is ISoftDeletable softDeletable)
        {
            softDeletable.IsDeleted = false;
        }
    }

    private void PopulateAuditFieldsOnUpdate(TEntity entity)
    {
        if (entity is IAuditable auditable)
        {
            auditable.ModifiedAt = DateTime.UtcNow;
            auditable.ModifiedBy = _auditContext?.CurrentUserId;
        }
    }

    private static void EnsureId(TEntity entity)
    {
        if (entity.Id == null)
            throw new InvalidOperationException("Entity Id must not be null");
    }

    #endregion
}
