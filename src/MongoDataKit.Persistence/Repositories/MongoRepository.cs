using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using MongoDataKit.Abstractions;
using MongoDataKit.Accessors.Extensions;
using MongoDataKit.Accessors.Filtering;
using MongoDataKit.Core.Interfaces;
using MongoDataKit.Core.Paging;
using MongoDataKit.Persistence.Entities;

namespace MongoDataKit.Persistence.Repositories;

public interface IMongoRepository<TEntity, in TId> : IRepository<TEntity, TId>
    where TEntity : MongoEntity<TId>
{
    Task<TEntity?> GetByIdAsync(IClientSessionHandle session, TId id);
    Task<IEnumerable<TEntity>> GetAllAsync(IClientSessionHandle session);
    Task<IPagedResult<TEntity>> FindPagedAsync(IQueryFilter<TEntity> filter);
    Task<IPagedResult<TEntity>> FindPagedAsync(IClientSessionHandle session, IQueryFilter<TEntity> filter);
    Task AddAsync(IClientSessionHandle session, TEntity entity);
    Task AddAsync(IClientSessionHandle session, IEnumerable<TEntity> entities);
    Task UpdateAsync(IClientSessionHandle session, TEntity entity);
    Task UpsertAsync(IClientSessionHandle session, TEntity entity);
    Task UpsertManyAsync(IClientSessionHandle session, IEnumerable<TEntity> entities);
    Task DeleteAsync(IClientSessionHandle session, TEntity entity);
    Task DeleteAsync(IClientSessionHandle session, TId id);
    Task DeleteManyAsync(IClientSessionHandle session, IEnumerable<TEntity> entities);
    Task DeleteManyAsync(IClientSessionHandle session, IEnumerable<TId> ids);
    Task<TResult> WithTransactionAsync<TResult>(
        Func<IClientSessionHandle, Task<TResult>> callback,
        TransactionOptions? options = null,
        CancellationToken ct = default);
}

public abstract class MongoRepository<TEntity, TId> : IMongoRepository<TEntity, TId>
    where TEntity : MongoEntity<TId>
{
    protected readonly IMongoDbContext Context;
    protected readonly IMongoCollection<TEntity> Collection;

    protected MongoRepository(IMongoDbContext context, IMongoCollection<TEntity> collection)
    {
        Context = context;
        Collection = collection;
    }

    public void Add(TEntity entity)
        => Context.AddCommand(s => Collection.InsertOneAsync(s, entity));

    public Task AddAsync(IClientSessionHandle session, TEntity entity)
        => Collection.InsertOneAsync(session, entity);

    public void Add(IEnumerable<TEntity> entities)
        => Context.AddCommand(s => Collection.InsertManyAsync(s, entities));

    public Task AddAsync(IClientSessionHandle session, IEnumerable<TEntity> entities)
        => Collection.InsertManyAsync(session, entities);

    public async Task<TEntity?> GetByIdAsync(TId id)
    {
        using var cursor = await Collection.FindAsync(
            Builders<TEntity>.Filter.Eq("_id", id));
        return await cursor.SingleOrDefaultAsync();
    }

    public async Task<TEntity?> GetByIdAsync(IClientSessionHandle session, TId id)
    {
        using var cursor = await Collection.FindAsync(session,
            Builders<TEntity>.Filter.Eq("_id", id));
        return await cursor.SingleOrDefaultAsync();
    }

    public async Task<IEnumerable<TEntity>> GetAllAsync()
    {
        using var cursor = await Collection.FindAsync(
            Builders<TEntity>.Filter.Empty);
        return await cursor.ToListAsync();
    }

    public async Task<IEnumerable<TEntity>> GetAllAsync(IClientSessionHandle session)
    {
        using var cursor = await Collection.FindAsync(session,
            Builders<TEntity>.Filter.Empty);
        return await cursor.ToListAsync();
    }

    public Task<IPagedResult<TEntity>> FindPagedAsync(IQueryFilter<TEntity> filter)
        => Collection.FindPagedAsync(filter);

    public Task<IPagedResult<TEntity>> FindPagedAsync(
        IClientSessionHandle session, IQueryFilter<TEntity> filter)
        => Collection.FindPagedAsync(session, filter);

    public void Update(TEntity entity)
    {
        EnsureId(entity);
        Context.AddCommand(s => Collection.ReplaceOneAsync(s,
            Builders<TEntity>.Filter.Eq("_id", entity.Id), entity));
    }

    public Task UpdateAsync(IClientSessionHandle session, TEntity entity)
    {
        EnsureId(entity);
        return Collection.ReplaceOneAsync(session,
            Builders<TEntity>.Filter.Eq("_id", entity.Id), entity);
    }

    public void Upsert(TEntity entity)
    {
        EnsureId(entity);
        Context.AddCommand(s => Collection.ReplaceOneAsync(s,
            Builders<TEntity>.Filter.Eq("_id", entity.Id), entity,
            new ReplaceOptions { IsUpsert = true }));
    }

    public Task UpsertAsync(IClientSessionHandle session, TEntity entity)
    {
        EnsureId(entity);
        return Collection.ReplaceOneAsync(session,
            Builders<TEntity>.Filter.Eq("_id", entity.Id), entity,
            new ReplaceOptions { IsUpsert = true });
    }

    public void UpsertMany(IEnumerable<TEntity> entities)
        => Context.AddCommand(s => Collection.UpsertManyAsync(s, entities));

    public Task UpsertManyAsync(IClientSessionHandle session, IEnumerable<TEntity> entities)
        => Collection.UpsertManyAsync(session, entities);

    public void Delete(TEntity entity)
    {
        EnsureId(entity);
        Delete(entity.Id!);
    }

    public Task DeleteAsync(IClientSessionHandle session, TEntity entity)
    {
        EnsureId(entity);
        return DeleteAsync(session, entity.Id!);
    }

    public void Delete(TId id)
        => Context.AddCommand(s => Collection.DeleteOneAsync(s,
            Builders<TEntity>.Filter.Eq("_id", id)));

    public Task DeleteAsync(IClientSessionHandle session, TId id)
        => Collection.DeleteOneAsync(session, Builders<TEntity>.Filter.Eq("_id", id));

    public void DeleteMany(IEnumerable<TEntity> entities)
    {
        var ids = entities.OfType<IEntity>().Select(e => e.Id);
        Context.AddCommand(s => Collection.DeleteManyByIdsAsync(s, ids));
    }

    public void DeleteMany(IEnumerable<TId> ids)
        => Context.AddCommand(s => Collection.DeleteManyByIdsAsync(s, ids.Cast<object>()));

    public Task DeleteManyAsync(IClientSessionHandle session, IEnumerable<TEntity> entities)
        => Collection.DeleteManyByIdsAsync(session,
            entities.OfType<IEntity>().Select(e => e.Id));

    public Task DeleteManyAsync(IClientSessionHandle session, IEnumerable<TId> ids)
        => Collection.DeleteManyByIdsAsync(session, ids.Cast<object>());

    public async Task<TResult> WithTransactionAsync<TResult>(
        Func<IClientSessionHandle, Task<TResult>> callback,
        TransactionOptions? options = null,
        CancellationToken ct = default)
    {
        using var session = await Collection.Database.Client.StartSessionAsync(
            new ClientSessionOptions { CausalConsistency = true }, ct);

        if (session.Client.Cluster.Description.Type != ClusterType.Standalone)
            return await session.WithTransactionAsync(
                (s, _) => callback(s), options, ct);

        return await callback(session);
    }

    private static void EnsureId(TEntity entity)
    {
        if (entity.Id == null)
            throw new InvalidOperationException("Entity Id must not be null");
    }
}
