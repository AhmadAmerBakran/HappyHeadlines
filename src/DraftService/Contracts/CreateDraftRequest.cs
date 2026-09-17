using System.ComponentModel.DataAnnotations;

namespace DraftService.Contracts;

public sealed class CreateDraftRequest
{
    [Required]
    [StringLength(200)]
    public string Title { get; init; } = string.Empty;

    [Required]
    public string Content { get; init; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Author { get; init; } = string.Empty;
}
