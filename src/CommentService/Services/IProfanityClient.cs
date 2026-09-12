using CommentService.Contracts;

namespace CommentService.Services;

public interface IProfanityClient
{
    Task<ProfanityFilterResponse?> FilterAsync(string text, CancellationToken cancellationToken);
}
