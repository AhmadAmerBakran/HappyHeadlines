using System.Net.Http.Json;
using CommentService.Contracts;
using Polly.CircuitBreaker;

namespace CommentService.Services;

public sealed class ProfanityClient : IProfanityClient
{
    private readonly HttpClient _httpClient;
    private readonly AsyncCircuitBreakerPolicy<HttpResponseMessage> _circuitBreaker;
    private readonly ILogger<ProfanityClient> _logger;

    public ProfanityClient(
        HttpClient httpClient,
        AsyncCircuitBreakerPolicy<HttpResponseMessage> circuitBreaker,
        ILogger<ProfanityClient> logger)
    {
        _httpClient = httpClient;
        _circuitBreaker = circuitBreaker;
        _logger = logger;
    }

    public async Task<ProfanityFilterResponse?> FilterAsync(
        string text,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _circuitBreaker.ExecuteAsync(
                ct => _httpClient.PostAsJsonAsync(
                    "api/profanity/filter",
                    new ProfanityFilterRequest { Text = text },
                    ct),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "ProfanityService returned status code {StatusCode}.",
                    (int)response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<ProfanityFilterResponse>(
                cancellationToken: cancellationToken);
        }
        catch (BrokenCircuitException)
        {
            _logger.LogWarning("ProfanityService circuit breaker is open.");
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "ProfanityService could not be reached.");
            return null;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "ProfanityService request timed out.");
            return null;
        }
    }
}
