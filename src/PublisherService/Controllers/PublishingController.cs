using HappyHeadlines.Messaging;
using Microsoft.AspNetCore.Mvc;
using PublisherService.Contracts;
using PublisherService.Services;

namespace PublisherService.Controllers;

[ApiController]
[Route("api/publishing")]
public sealed class PublishingController(
    ArticlePublisher publisher,
    ILogger<PublishingController> logger) : ControllerBase
{
    private static readonly HashSet<string> AllowedScopes = new(StringComparer.Ordinal)
    {
        "africa",
        "antarctica",
        "asia",
        "australia",
        "europe",
        "north-america",
        "south-america",
        "global"
    };

    [HttpPost]
    public async Task<IActionResult> Publish(
        PublishArticleRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(new { error = "Title and content are required." });
        }

        var scope = NormalizeScope(request.Scope);
        if (scope is null)
        {
            return BadRequest(new { error = $"Unknown scope '{request.Scope}'." });
        }

        var article = new PublishedArticle
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Content = request.Content,
            Source = string.IsNullOrWhiteSpace(request.Source) ? null : request.Source.Trim(),
            Scope = scope,
            PublishedAtUtc = DateTime.UtcNow
        };

        try
        {
            await publisher.PublishAsync(article, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not publish article {ArticleId}.", article.Id);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                error = "The article queue is unavailable."
            });
        }

        return Accepted(new
        {
            article.Id,
            article.Scope,
            article.PublishedAtUtc
        });
    }

    private static string? NormalizeScope(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var scope = value.Trim().ToLowerInvariant()
            .Replace('_', '-')
            .Replace(' ', '-');

        scope = scope switch
        {
            "northamerica" => "north-america",
            "southamerica" => "south-america",
            "oceania" => "australia",
            _ => scope
        };

        return AllowedScopes.Contains(scope) ? scope : null;
    }
}
