using MongoDataKit.Abstractions;

namespace MongoDataKit.Persistence;

public sealed class MongoUnitOfWork : IUnitOfWork
{
    private readonly IMongoDbContext _context;

    public MongoUnitOfWork(IMongoDbContext context) => _context = context;

    public async Task<bool> CommitAsync()
    {
        var expected = _context.PendingCommands;
        var executed = await _context.SaveChangesAsync();
        return executed == expected;
    }
}
