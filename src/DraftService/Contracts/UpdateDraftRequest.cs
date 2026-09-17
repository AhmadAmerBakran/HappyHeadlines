using System.ComponentModel.DataAnnotations;

namespace DraftService.Contracts;

public sealed class UpdateDraftRequest
{
    [Required]
    [StringLength(200)]
    public string Title { get; init; } = string.Empty;

    [Required]
    public string Content { get; init; } = string.Empty;
}
