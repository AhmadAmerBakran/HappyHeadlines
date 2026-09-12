using ProfanityService.Contracts;

namespace ProfanityService.Services;

public interface IProfanityFilter
{
    Task<FilterTextResponse> FilterAsync(string text, CancellationToken cancellationToken);
}
