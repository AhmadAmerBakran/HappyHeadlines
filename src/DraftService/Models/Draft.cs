namespace DraftService.Models;

public sealed class Draft
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public required string Content { get; set; }
    public required string Author { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
