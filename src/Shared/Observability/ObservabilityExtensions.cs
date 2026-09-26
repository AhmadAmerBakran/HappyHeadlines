using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace HappyHeadlines.Observability;

public static class ObservabilityExtensions
{
    public static WebApplicationBuilder AddHappyHeadlinesObservability(
        this WebApplicationBuilder builder,
        string serviceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        var resourceBuilder = ResourceBuilder
            .CreateDefault()
            .AddService(serviceName);

        var endpoint = TryGetOtlpEndpoint(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        builder.Services.AddSingleton(new ObservabilityServiceInfo(serviceName));

        builder.Logging.AddOpenTelemetry(options =>
        {
            options.SetResourceBuilder(resourceBuilder);
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;
            options.ParseStateValues = true;

            if (endpoint is not null)
            {
                options.AddOtlpExporter(exporter => exporter.Endpoint = endpoint);
            }
        });

        builder.Services
            .AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSource(HappyHeadlinesDiagnostics.ActivitySourceName);

                if (endpoint is not null)
                {
                    tracing.AddOtlpExporter(exporter => exporter.Endpoint = endpoint);
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(resourceBuilder)
                    .AddMeter(HappyHeadlinesDiagnostics.MeterName);

                if (endpoint is not null)
                {
                    metrics.AddOtlpExporter(exporter => exporter.Endpoint = endpoint);
                }
            });

        return builder;
    }

    public static IApplicationBuilder UseHappyHeadlinesRequestLogging(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var loggerFactory = context.RequestServices.GetRequiredService<ILoggerFactory>();
            var serviceInfo = context.RequestServices.GetRequiredService<ObservabilityServiceInfo>();
            var logger = loggerFactory.CreateLogger("HttpRequest");
            var stopwatch = Stopwatch.StartNew();
            var statusCode = 500;

            using var scope = logger.BeginScope(new Dictionary<string, object?>
            {
                ["TraceId"] = Activity.Current?.TraceId.ToString(),
                ["RequestId"] = context.TraceIdentifier
            });

            try
            {
                await next();
                statusCode = context.Response.StatusCode;

                logger.LogInformation(
                    "HTTP {Method} {Path} completed with {StatusCode} in {ElapsedMilliseconds} ms",
                    context.Request.Method,
                    context.Request.Path.Value,
                    statusCode,
                    stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "HTTP {Method} {Path} failed after {ElapsedMilliseconds} ms",
                    context.Request.Method,
                    context.Request.Path.Value,
                    stopwatch.ElapsedMilliseconds);

                throw;
            }
            finally
            {
                var tags = new TagList
                {
                    { "service", serviceInfo.Name },
                    { "http_method", context.Request.Method },
                    { "status_code", statusCode }
                };

                HappyHeadlinesDiagnostics.HttpRequests.Add(1, tags);
                HappyHeadlinesDiagnostics.HttpRequestDuration.Record(
                    stopwatch.Elapsed.TotalMilliseconds,
                    tags);
            }
        });
    }

    private static Uri? TryGetOtlpEndpoint(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var endpoint)
            ? endpoint
            : null;
    }

    private sealed record ObservabilityServiceInfo(string Name);
}
