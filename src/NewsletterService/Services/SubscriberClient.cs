using System.Net.Http.Json;

namespace NewsletterService.Services;

public sealed class SubscriberClient(HttpClient client, IConfiguration configuration)
{
    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken)
    {
        using var request = CreateRequest("api/subscribers/rollout/status");
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var status = await response.Content.ReadFromJsonAsync<SubscriberFeatureStatus>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Subscriber feature status is missing.");

        return status.Enabled;
    }

    public async Task<string[]> GetSubscribersAsync(CancellationToken cancellationToken)
    {
        using var request = CreateRequest("api/subscribers");
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<string[]>(
            cancellationToken: cancellationToken) ?? [];
    }

    private HttpRequestMessage CreateRequest(string path)
    {
        var key = configuration["Subscriber:InternalKey"]
            ?? throw new InvalidOperationException("Subscriber:InternalKey is missing.");

        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Subscriber-Internal-Key", key);
        return request;
    }

    private sealed record SubscriberFeatureStatus(bool Enabled);
}
