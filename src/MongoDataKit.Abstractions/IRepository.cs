using MongoDB.Bson.Serialization.Attributes;

namespace MongoDataKit.Abstractions;

public abstract class Entity<TId>
{
    [BsonId]
    public TId? Id { get; set; }
}

public interface IReadRepository<TEntity, in TId> where TEntity : Entity<TId>
{
    Task<TEntity?> GetByIdAsync(TId id);
    Task<IEnumerable<TEntity>> GetAllAsync();
}

public interface IRepository<TEntity, in TId> : IReadRepository<TEntity, TId>
    where TEntity : Entity<TId>
{
    void Add(TEntity entity);
    void Add(IEnumerable<TEntity> entities);
    void Update(TEntity entity);
    void Upsert(TEntity entity);
    void UpsertMany(IEnumerable<TEntity> entities);
    void Delete(TEntity entity);
    void Delete(TId id);
    void DeleteMany(IEnumerable<TEntity> entities);
    void DeleteMany(IEnumerable<TId> ids);
}

public interface IUnitOfWork
{
    Task<bool> CommitAsync();
}
