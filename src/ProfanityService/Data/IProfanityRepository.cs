namespace ProfanityService.Data;

public interface IProfanityRepository
{
    Task<IReadOnlyList<string>> GetWordsAsync(CancellationToken cancellationToken);
}
