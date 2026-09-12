namespace ProfanityService.Contracts;

public sealed class FilterTextResponse
{
    public required string FilteredText { get; init; }
    public bool HadProfanity { get; init; }
}
