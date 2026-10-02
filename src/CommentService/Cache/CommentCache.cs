using System.Text.Json;
using CommentService.Models;
using HappyHeadlines.Observability;
using StackExchange.Redis;

namespace CommentService.Cache;

public sealed class CommentCache(
    IConnectionMultiplexer redis,
    ILogger<CommentCache> logger)
{
    private const string CacheName = "comment";
    private const string LruKey = "comment-cache:lru";
    private const int MaxArticles = 30;
    private readonly IDatabase _database = redis.GetDatabase();

    public async Task<IReadOnlyList<Comment>?> GetAsync(Guid articleId)
    {
        try
        {
            var value = await _database.StringGetAsync(ArticleKey(articleId));
            if (value.IsNull)
            {
                HappyHeadlinesDiagnostics.RecordCacheRequest(CacheName, false);
                return null;
            }

            await TouchAsync(articleId);
            HappyHeadlinesDiagnostics.RecordCacheRequest(CacheName, true);
            return JsonSerializer.Deserialize<List<Comment>>(value.ToString()) ?? [];
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Comment cache lookup failed for article {ArticleId}", articleId);
            HappyHeadlinesDiagnostics.RecordCacheRequest(CacheName, false);
            return null;
        }
    }

    public async Task SetAsync(Guid articleId, IReadOnlyList<Comment> comments)
    {
        try
        {
            await _database.StringSetAsync(
                ArticleKey(articleId),
                JsonSerializer.Serialize(comments));

            await TouchAsync(articleId);
            await TrimAsync();
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Comment cache update failed for article {ArticleId}", articleId);
        }
    }

    public async Task InvalidateAsync(Guid articleId)
    {
        try
        {
            await _database.KeyDeleteAsync(ArticleKey(articleId));
            await _database.SortedSetRemoveAsync(LruKey, articleId.ToString("N"));
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Comment cache invalidation failed for article {ArticleId}", articleId);
        }
    }

    private async Task TouchAsync(Guid articleId)
    {
        await _database.SortedSetAddAsync(
            LruKey,
            articleId.ToString("N"),
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    private async Task TrimAsync()
    {
        var count = await _database.SortedSetLengthAsync(LruKey);
        if (count <= MaxArticles)
        {
            return;
        }

        var removeCount = count - MaxArticles;
        var oldest = await _database.SortedSetRangeByRankAsync(
            LruKey,
            0,
            removeCount - 1,
            Order.Ascending);

        if (oldest.Length == 0)
        {
            return;
        }

        var keys = oldest
            .Select(value => Guid.Parse(value.ToString()))
            .Select(id => (RedisKey)ArticleKey(id))
            .ToArray();

        await _database.KeyDeleteAsync(keys);
        await _database.SortedSetRemoveAsync(LruKey, oldest);
    }

    private static string ArticleKey(Guid articleId) => $"comment-cache:article:{articleId:N}";
}
