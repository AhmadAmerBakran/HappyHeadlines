using ArticleService.Models;

namespace ArticleService.Data;

public interface IArticleRepository
{
    Task<Article> CreateAsync(Article article, CancellationToken cancellationToken);
    Task<Article?> GetByIdAsync(string scope, Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Article>> GetAllAsync(string scope, int limit, CancellationToken cancellationToken);
    Task<Article?> UpdateAsync(string scope, Guid id, string title, string content, string? source, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(string scope, Guid id, CancellationToken cancellationToken);
}
