namespace ArticleService.Models;

public sealed class Article
{
    public Guid Id { get; init; }
    public required string Title { get; init; }
    public required string Content { get; init; }
    public string? Source { get; init; }
    public required string Scope { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}
