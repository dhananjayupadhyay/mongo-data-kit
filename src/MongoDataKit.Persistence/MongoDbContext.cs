using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;

namespace MongoDataKit.Persistence;

public interface IMongoDbContext
{
    void AddCommand(Func<IClientSessionHandle, Task> command);
    int PendingCommands { get; }
    Task<int> SaveChangesAsync();
}

public class MongoDbContext : IMongoDbContext
{
    private readonly IMongoClient _client;
    private readonly List<Func<IClientSessionHandle, Task>> _commands = new();

    public MongoDbContext(IMongoClient client) => _client = client;

    public int PendingCommands => _commands.Count;

    public void AddCommand(Func<IClientSessionHandle, Task> command)
        => _commands.Add(command);

    public async Task<int> SaveChangesAsync()
    {
        if (_commands.Count == 0) return 0;

        var supportsTransactions =
            _client.Cluster.Description.Type != ClusterType.Standalone;
        var executed = 0;

        try
        {
            using var session = await _client.StartSessionAsync(
                new ClientSessionOptions { CausalConsistency = true });

            if (supportsTransactions) session.StartTransaction();

            foreach (var cmd in _commands)
            {
                await cmd(session);
                executed++;
            }

            if (supportsTransactions)
                await session.CommitTransactionAsync();
        }
        finally
        {
            _commands.Clear();
        }
        return executed;
    }
}
