using ArticleService.Data;
using StackExchange.Redis;

namespace ArticleService.Cache;

public sealed class ArticleCacheRefreshWorker(
    IServiceScopeFactory scopeFactory,
    ArticleCache cache,
    IConnectionMultiplexer redis,
    IConfiguration configuration,
    ILogger<ArticleCacheRefreshWorker> logger) : BackgroundService
{
    private const string RefreshLockKey = "article-cache:refresh-lock";
    private readonly IDatabase _database = redis.GetDatabase();
    private readonly string _lockValue = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    private readonly TimeSpan _refreshInterval = TimeSpan.FromSeconds(
        ReadPositiveInt(configuration["Cache:ArticleRefreshSeconds"], 60));

    private readonly TimeSpan _timeToLive = TimeSpan.FromSeconds(
        ReadPositiveInt(configuration["Cache:ArticleTtlSeconds"], 180));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RefreshAsync(stoppingToken);

        using var timer = new PeriodicTimer(_refreshInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RefreshAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            var lockDuration = _refreshInterval > TimeSpan.FromSeconds(10)
                ? _refreshInterval - TimeSpan.FromSeconds(5)
                : _refreshInterval;

            if (!await _database.LockTakeAsync(RefreshLockKey, _lockValue, lockDuration))
            {
                return;
            }

            using var scope = scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IArticleRepository>();
            var sinceUtc = DateTime.UtcNow.AddDays(-14);
            var articles = await repository.GetRecentAsync("global", sinceUtc, cancellationToken);

            if (await cache.ReplaceAsync(articles, _timeToLive))
            {
                logger.LogInformation(
                    "Article cache refreshed with {ArticleCount} global articles",
                    articles.Count);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Article cache refresh could not complete");
        }
    }

    private static int ReadPositiveInt(string? value, int fallback)
    {
        return int.TryParse(value, out var parsed) && parsed > 0
            ? parsed
            : fallback;
    }
}
