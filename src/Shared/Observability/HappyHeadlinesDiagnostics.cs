using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace HappyHeadlines.Observability;

public static class HappyHeadlinesDiagnostics
{
    public const string ActivitySourceName = "HappyHeadlines";
    public const string MeterName = "HappyHeadlines";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    public static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> HttpRequests = Meter.CreateCounter<long>(
        "happyheadlines_http_requests",
        description: "Completed HTTP requests");

    public static readonly Histogram<double> HttpRequestDuration = Meter.CreateHistogram<double>(
        "happyheadlines_http_request_duration_ms",
        description: "HTTP request duration in milliseconds");

    public static readonly Counter<long> CacheRequests = Meter.CreateCounter<long>(
        "happyheadlines_cache_requests",
        description: "Cache lookups grouped by cache and result");

    public static void RecordCacheRequest(string cacheName, bool hit)
    {
        var tags = new TagList
        {
            { "cache", cacheName },
            { "result", hit ? "hit" : "miss" }
        };

        CacheRequests.Add(1, tags);
    }

    public static Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
        => ActivitySource.StartActivity(name, kind);

    public static Activity? StartActivity(
        string name,
        ActivityKind kind,
        ActivityContext parentContext)
        => ActivitySource.StartActivity(name, kind, parentContext);
}
