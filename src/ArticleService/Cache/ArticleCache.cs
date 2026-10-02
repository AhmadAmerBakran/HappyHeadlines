using System.Text.Json;
using ArticleService.Models;
using HappyHeadlines.Observability;
using StackExchange.Redis;

namespace ArticleService.Cache;

public sealed class ArticleCache(
    IConnectionMultiplexer redis,
    ILogger<ArticleCache> logger)
{
    private const string CacheName = "article";
    private const string RecentArticlesKey = "article-cache:recent";
    private readonly IDatabase _database = redis.GetDatabase();

    public async Task<Article?> GetByIdAsync(Guid id)
    {
        try
        {
            var value = await _database.StringGetAsync(ArticleKey(id));
            if (value.IsNull)
            {
                HappyHeadlinesDiagnostics.RecordCacheRequest(CacheName, false);
                return null;
            }

            HappyHeadlinesDiagnostics.RecordCacheRequest(CacheName, true);
            return JsonSerializer.Deserialize<Article>(value.ToString());
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Article cache lookup failed for {ArticleId}", id);
            HappyHeadlinesDiagnostics.RecordCacheRequest(CacheName, false);
            return null;
        }
    }

    public async Task<IReadOnlyList<Article>?> GetRecentAsync(int limit)
    {
        try
        {
            var value = await _database.StringGetAsync(RecentArticlesKey);
            if (value.IsNull)
            {
                HappyHeadlinesDiagnostics.RecordCacheRequest(CacheName, false);
                return null;
            }

            var articles = JsonSerializer.Deserialize<List<Article>>(value.ToString()) ?? [];
            if (articles.Count < limit)
            {
                HappyHeadlinesDiagnostics.RecordCacheRequest(CacheName, false);
                return null;
            }

            HappyHeadlinesDiagnostics.RecordCacheRequest(CacheName, true);
            return articles.Take(limit).ToList();
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Article cache lookup failed");
            HappyHeadlinesDiagnostics.RecordCacheRequest(CacheName, false);
            return null;
        }
    }

    public async Task<bool> ReplaceAsync(
        IReadOnlyList<Article> articles,
        TimeSpan timeToLive)
    {
        try
        {
            await _database.StringSetAsync(
                RecentArticlesKey,
                JsonSerializer.Serialize(articles),
                timeToLive);

            var writes = articles.Select(article =>
                _database.StringSetAsync(
                    ArticleKey(article.Id),
                    JsonSerializer.Serialize(article),
                    timeToLive));

            await Task.WhenAll(writes);
            return true;
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Article cache refresh failed");
            return false;
        }
    }

    public async Task InvalidateAsync(Guid? articleId = null)
    {
        try
        {
            if (articleId.HasValue)
            {
                await _database.KeyDeleteAsync(ArticleKey(articleId.Value));
            }

            await _database.KeyDeleteAsync(RecentArticlesKey);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Article cache invalidation failed");
        }
    }

    private static string ArticleKey(Guid id) => $"article-cache:item:{id:N}";
}
