using ArticleService.Contracts;
using ArticleService.Data;
using ArticleService.Models;
using Microsoft.AspNetCore.Mvc;

namespace ArticleService.Controllers;

[ApiController]
[Route("api/articles")]
public sealed class ArticlesController(IArticleRepository repository) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<Article>> Create(
        CreateArticleRequest request,
        CancellationToken cancellationToken)
    {
        if (!ArticleScopes.TryNormalize(request.Scope, out var scope))
        {
            return InvalidScope(request.Scope);
        }

        var now = DateTime.UtcNow;
        var article = new Article
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Content = request.Content,
            Source = string.IsNullOrWhiteSpace(request.Source) ? null : request.Source.Trim(),
            Scope = scope,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await repository.CreateAsync(article, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { scope = article.Scope, id = article.Id },
            article);
    }

    [HttpGet("{scope}/{id:guid}")]
    public async Task<ActionResult<Article>> GetById(
        string scope,
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!ArticleScopes.TryNormalize(scope, out var normalizedScope))
        {
            return InvalidScope(scope);
        }

        var article = await repository.GetByIdAsync(normalizedScope, id, cancellationToken);
        return article is null ? NotFound() : Ok(article);
    }

    [HttpGet("{scope}")]
    public async Task<ActionResult<IReadOnlyList<Article>>> GetAll(
        string scope,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (!ArticleScopes.TryNormalize(scope, out var normalizedScope))
        {
            return InvalidScope(scope);
        }

        limit = Math.Clamp(limit, 1, 100);
        var articles = await repository.GetAllAsync(normalizedScope, limit, cancellationToken);
        return Ok(articles);
    }

    [HttpPut("{scope}/{id:guid}")]
    public async Task<ActionResult<Article>> Update(
        string scope,
        Guid id,
        UpdateArticleRequest request,
        CancellationToken cancellationToken)
    {
        if (!ArticleScopes.TryNormalize(scope, out var normalizedScope))
        {
            return InvalidScope(scope);
        }

        var article = await repository.UpdateAsync(
            normalizedScope,
            id,
            request.Title.Trim(),
            request.Content,
            string.IsNullOrWhiteSpace(request.Source) ? null : request.Source.Trim(),
            cancellationToken);

        return article is null ? NotFound() : Ok(article);
    }

    [HttpDelete("{scope}/{id:guid}")]
    public async Task<IActionResult> Delete(
        string scope,
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!ArticleScopes.TryNormalize(scope, out var normalizedScope))
        {
            return InvalidScope(scope);
        }

        var deleted = await repository.DeleteAsync(normalizedScope, id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    private BadRequestObjectResult InvalidScope(string? scope)
    {
        return BadRequest(new
        {
            error = $"Unknown scope '{scope}'.",
            allowedScopes = ArticleScopes.All
        });
    }
}
