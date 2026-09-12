using CommentService.Models;

namespace CommentService.Data;

public interface ICommentRepository
{
    Task AddAsync(Comment comment, CancellationToken cancellationToken);
    Task<Comment?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Comment>> GetPublishedByArticleAsync(Guid articleId, CancellationToken cancellationToken);
    Task MarkPublishedAsync(Guid id, string filteredContent, CancellationToken cancellationToken);
}
