using System.ComponentModel.DataAnnotations;

namespace CommentService.Contracts;

public sealed class CreateCommentRequest
{
    public Guid ArticleId { get; init; }

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Author { get; init; } = string.Empty;

    [Required]
    [StringLength(4000, MinimumLength = 1)]
    public string Content { get; init; } = string.Empty;
}
