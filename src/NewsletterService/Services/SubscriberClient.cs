using System.Net.Http.Json;

namespace NewsletterService.Services;

public sealed class SubscriberClient(HttpClient client, IConfiguration configuration)
{
    public async Task<string[]> GetSubscribersAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/subscribers");
        var key = configuration["Subscriber:InternalKey"]
            ?? throw new InvalidOperationException("Subscriber:InternalKey is missing.");
        request.Headers.Add("X-Subscriber-Internal-Key", key);

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<string[]>(cancellationToken: cancellationToken)
            ?? [];
    }
}
