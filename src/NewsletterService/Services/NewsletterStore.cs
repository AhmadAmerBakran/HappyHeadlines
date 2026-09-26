using System.Collections.Concurrent;
using HappyHeadlines.Messaging;

namespace NewsletterService.Services;

public sealed class NewsletterStore
{
    private readonly ConcurrentQueue<PublishedArticle> _articles = new();

    public void Add(PublishedArticle article)
    {
        _articles.Enqueue(article);

        while (_articles.Count > 100)
        {
            _articles.TryDequeue(out _);
        }
    }

    public IReadOnlyList<PublishedArticle> Latest(int limit)
    {
        return _articles
            .ToArray()
            .Reverse()
            .Take(Math.Clamp(limit, 1, 50))
            .ToArray();
    }
}
