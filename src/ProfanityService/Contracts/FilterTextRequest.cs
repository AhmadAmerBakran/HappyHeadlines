using System.ComponentModel.DataAnnotations;

namespace ProfanityService.Contracts;

public sealed class FilterTextRequest
{
    [Required]
    [StringLength(4000, MinimumLength = 1)]
    public string Text { get; init; } = string.Empty;
}
