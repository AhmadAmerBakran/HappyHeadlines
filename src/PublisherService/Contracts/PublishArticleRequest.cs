namespace PublisherService.Contracts;

public sealed class PublishArticleRequest
{
    public required string Title { get; init; }
    public required string Content { get; init; }
    public string? Source { get; init; }
    public required string Scope { get; init; }
}
