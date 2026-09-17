using DraftService.Models;

namespace DraftService.Data;

public interface IDraftRepository
{
    Task<IReadOnlyList<Draft>> GetAllAsync(CancellationToken cancellationToken);
    Task<Draft?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task CreateAsync(Draft draft, CancellationToken cancellationToken);
    Task<bool> UpdateAsync(Draft draft, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
