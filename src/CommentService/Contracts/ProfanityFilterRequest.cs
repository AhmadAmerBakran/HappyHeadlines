namespace CommentService.Contracts;

public sealed class ProfanityFilterRequest
{
    public required string Text { get; init; }
}
