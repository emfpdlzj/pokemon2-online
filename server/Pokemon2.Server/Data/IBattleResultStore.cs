namespace Pokemon2.Server.Data;

public interface IBattleResultStore
{
    Task SaveAsync(BattleResult result, CancellationToken cancellationToken);
}

public sealed class NoopBattleResultStore : IBattleResultStore
{
    public Task SaveAsync(BattleResult result, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

public sealed class EfBattleResultStore : IBattleResultStore
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EfBattleResultStore> _logger;

    public EfBattleResultStore(IServiceScopeFactory scopeFactory, ILogger<EfBattleResultStore> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task SaveAsync(BattleResult result, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
            db.BattleResults.Add(result);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist battle result for room {RoomId}.", result.RoomId);
        }
    }
}
