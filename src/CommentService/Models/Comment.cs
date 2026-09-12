namespace CommentService.Models;

public sealed class Comment
{
    public Guid Id { get; init; }
    public Guid ArticleId { get; init; }
    public required string Author { get; init; }
    public required string Content { get; init; }
    public required string ModerationStatus { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
