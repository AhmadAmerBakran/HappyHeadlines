using CommentService.Contracts;
using CommentService.Data;
using CommentService.Models;
using CommentService.Services;
using Microsoft.AspNetCore.Mvc;

namespace CommentService.Controllers;

[ApiController]
[Route("api/comments")]
public sealed class CommentsController(
    ICommentRepository repository,
    IProfanityClient profanityClient) : ControllerBase
{
    [HttpGet("article/{articleId:guid}")]
    public async Task<ActionResult<IReadOnlyList<Comment>>> GetByArticle(
        Guid articleId,
        CancellationToken cancellationToken)
    {
        var comments = await repository.GetPublishedByArticleAsync(articleId, cancellationToken);
        return Ok(comments);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateCommentRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ArticleId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(request.ArticleId), "ArticleId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Author))
        {
            ModelState.AddModelError(nameof(request.Author), "Author is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            ModelState.AddModelError(nameof(request.Content), "Content is required.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var moderationResult = await profanityClient.FilterAsync(request.Content, cancellationToken);

        var comment = new Comment
        {
            Id = Guid.NewGuid(),
            ArticleId = request.ArticleId,
            Author = request.Author.Trim(),
            Content = moderationResult?.FilteredText ?? request.Content,
            ModerationStatus = moderationResult is null ? "pending" : "published",
            CreatedAtUtc = DateTime.UtcNow
        };

        await repository.AddAsync(comment, cancellationToken);

        if (moderationResult is null)
        {
            return Accepted(new
            {
                comment.Id,
                comment.ArticleId,
                status = comment.ModerationStatus,
                message = "Moderation is unavailable, so the comment is stored but not published."
            });
        }

        return CreatedAtAction(
            nameof(GetByArticle),
            new { articleId = comment.ArticleId },
            comment);
    }

    [HttpPost("{id:guid}/moderate")]
    public async Task<IActionResult> RetryModeration(
        Guid id,
        CancellationToken cancellationToken)
    {
        var comment = await repository.GetByIdAsync(id, cancellationToken);
        if (comment is null)
        {
            return NotFound();
        }

        if (comment.ModerationStatus == "published")
        {
            return Ok(comment);
        }

        var moderationResult = await profanityClient.FilterAsync(comment.Content, cancellationToken);
        if (moderationResult is null)
        {
            return Accepted(new
            {
                comment.Id,
                status = "pending",
                message = "Moderation is still unavailable."
            });
        }

        await repository.MarkPublishedAsync(
            comment.Id,
            moderationResult.FilteredText,
            cancellationToken);

        return Ok(new Comment
        {
            Id = comment.Id,
            ArticleId = comment.ArticleId,
            Author = comment.Author,
            Content = moderationResult.FilteredText,
            ModerationStatus = "published",
            CreatedAtUtc = comment.CreatedAtUtc
        });
    }
}
