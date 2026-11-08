# Week 45: Sustainability

We chose three GAF tactics that had not been covered in the project. The Redis caches from week 40 and the existing sharding setup were left unchanged.

## Reduce Network Package Size

The existing article list query includes the full article text even when only titles are needed.

Before (`ArticleRepository.cs`):

~~~csharp
SELECT id, title, content, source, scope, created_at_utc, updated_at_utc
FROM articles
ORDER BY created_at_utc DESC
LIMIT @limit;
~~~

After, the new headline query reads only the columns used by the list:

~~~csharp
SELECT id, title, source, scope, created_at_utc
FROM articles
ORDER BY created_at_utc DESC
LIMIT @limit;
~~~

The old article endpoints still return the same data. We added `GET /api/articles/{scope}/headlines` for the smaller response. ArticleService and WebApp also use Brotli or Gzip compression when a client requests it, with the fastest compression level to limit CPU overhead.

## Defer Fetching/Lazy fetch

The original list endpoint returns complete articles in one response.

Before (`ArticlesController.cs`):

~~~csharp
var articles = await repository.GetAllAsync(normalizedScope, limit, cancellationToken);
return Ok(articles);
~~~

The new `/headlines` page requests only the list first. An article's content is loaded after the reader selects it.

After (`WebApp/Program.cs`, page script):

~~~javascript
const response = await fetch('/api/headlines');
const headlines = await response.json();

// Article text is requested when a headline is opened.
button.addEventListener('click', () => showArticle(headline.id));
~~~

~~~javascript
const response = await fetch('/api/headlines/' + encodeURIComponent(id));
const article = await response.json();
document.getElementById('articleContent').textContent = article.content;
~~~

This avoids transferring the content of articles that the reader never opens.

## Reduce Logging

Health checks ran through the same information logging as regular requests.

Before (`ObservabilityExtensions.cs`):

~~~csharp
logger.LogInformation(
    "HTTP {Method} {Path} completed with {StatusCode} in {ElapsedMilliseconds} ms",
    context.Request.Method,
    context.Request.Path.Value,
    statusCode,
    stopwatch.ElapsedMilliseconds);
~~~

After:

~~~csharp
if (statusCode >= 400
    || !string.Equals(context.Request.Path.Value, "/health", StringComparison.OrdinalIgnoreCase))
{
    logger.LogInformation(
        "HTTP {Method} {Path} completed with {StatusCode} in {ElapsedMilliseconds} ms",
        context.Request.Method,
        context.Request.Path.Value,
        statusCode,
        stopwatch.ElapsedMilliseconds);
}
~~~

Successful `/health` requests no longer create information log entries. Failed requests still log, and the request counters and duration metrics continue to be recorded.

## Checking the changes

Start the usual Docker stack and open `http://localhost:8085/headlines`. The Network tab should show the headline list request first and the full article request only after clicking a title.

For compression, use `curl.exe --compressed -i -H "Accept-Encoding: gzip" http://localhost:8080/api/articles/global/headlines?limit=20` and check the `Content-Encoding` response header.

Repeat `GET /health` several times and check that there are no new information logs for successful health checks. Errors and the existing Grafana metrics should still be available.

These changes target network transfer and avoidable logging. We have not measured a direct reduction in electricity use or CO₂.
