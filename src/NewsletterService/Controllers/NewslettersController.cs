using Microsoft.AspNetCore.Mvc;
using NewsletterService.Services;

namespace NewsletterService.Controllers;

[ApiController]
[Route("api/newsletters")]
public sealed class NewslettersController(
    NewsletterStore store,
    ArticleClient articleClient,
    SubscriberClient subscriberClient,
    NewsletterMailer mailer,
    IConfiguration configuration) : ControllerBase
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

    [HttpPost("send")]
    public async Task<IActionResult> Send(NewsletterSendRequest request, CancellationToken cancellationToken)
    {
        var expected = configuration["Newsletter:AdminKey"];
        if (string.IsNullOrWhiteSpace(expected)
            || !Request.Headers.TryGetValue("X-Newsletter-Admin-Key", out var key)
            || key.Count != 1
            || !string.Equals(key[0], expected, StringComparison.Ordinal))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Subject) || request.Subject.Length > 200
            || string.IsNullOrWhiteSpace(request.Body) || request.Body.Length > 4000)
        {
            return BadRequest(new { error = "Subject and body are required." });
        }

        try
        {
            var emails = await subscriberClient.GetSubscribersAsync(cancellationToken);
            foreach (var email in emails)
            {
                await mailer.SendAsync(email, request.Subject, request.Body);
            }

            return Ok(new { sent = emails.Length });
        }
        catch (HttpRequestException)
        {
            return StatusCode(503, new { error = "The subscriber service is unavailable." });
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(503, new { error = "The subscriber service timed out." });
        }
        catch (System.Net.Mail.SmtpException)
        {
            return StatusCode(503, new { error = "The mail server is unavailable." });
        }
    }
}
