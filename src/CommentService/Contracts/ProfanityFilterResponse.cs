namespace CommentService.Contracts;

public sealed class ProfanityFilterResponse
{
    public required string FilteredText { get; init; }
    public bool HadProfanity { get; init; }
}
