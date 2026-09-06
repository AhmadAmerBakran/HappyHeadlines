using System.ComponentModel.DataAnnotations;

namespace ArticleService.Contracts;

public sealed class UpdateArticleRequest
{
    [Required, MaxLength(200)]
    public string Title { get; init; } = string.Empty;

    [Required]
    public string Content { get; init; } = string.Empty;

    [MaxLength(500)]
    public string? Source { get; init; }
}
