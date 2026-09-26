using Microsoft.AspNetCore.Mvc;
using NewsletterService.Services;

namespace NewsletterService.Controllers;

[ApiController]
[Route("api/newsletters")]
public sealed class NewslettersController(
    NewsletterStore store,
    ArticleClient articleClient) : ControllerBase
{
    [HttpGet("latest")]
    public IActionResult Latest([FromQuery] int limit = 10)
    {
        return Ok(store.Latest(limit));
    }

    [HttpGet("daily/{scope}/{id:guid}")]
    public async Task<IActionResult> Daily(
        string scope,
        Guid id,
        CancellationToken cancellationToken)
    {
        var article = await articleClient.GetArticleAsync(scope, id, cancellationToken);
        if (article is null)
        {
            return NotFound();
        }

        return Ok(new
        {
            subject = "Daily Happy Headlines",
            article
        });
    }
}
