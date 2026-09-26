using System.Text.Json;

namespace NewsletterService.Services;

public sealed class ArticleClient(HttpClient httpClient)
{
    public async Task<JsonElement?> GetArticleAsync(
        string scope,
        Guid id,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            $"api/articles/{Uri.EscapeDataString(scope)}/{id}",
            cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return document.RootElement.Clone();
    }
}
