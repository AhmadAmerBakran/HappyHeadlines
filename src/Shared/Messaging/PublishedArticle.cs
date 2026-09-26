namespace HappyHeadlines.Messaging;

public sealed class PublishedArticle
{
    public Guid Id { get; init; }
    public required string Title { get; init; }
    public required string Content { get; init; }
    public string? Source { get; init; }
    public required string Scope { get; init; }
    public DateTime PublishedAtUtc { get; init; }
}
