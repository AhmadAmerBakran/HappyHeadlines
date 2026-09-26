using System.Diagnostics;
using System.Text;
using RabbitMQ.Client;

namespace HappyHeadlines.Messaging;

public static class RabbitMqTraceContext
{
    private const string TraceParent = "traceparent";
    private const string TraceState = "tracestate";

    public static void Inject(IBasicProperties properties)
    {
        var activity = Activity.Current;
        if (activity?.Id is null)
        {
            return;
        }

        properties.Headers ??= new Dictionary<string, object>();
        properties.Headers[TraceParent] = Encoding.UTF8.GetBytes(activity.Id);

        if (!string.IsNullOrWhiteSpace(activity.TraceStateString))
        {
            properties.Headers[TraceState] = Encoding.UTF8.GetBytes(activity.TraceStateString);
        }
    }

    public static ActivityContext Extract(IBasicProperties properties)
    {
        var traceParent = ReadHeader(properties.Headers, TraceParent);
        if (string.IsNullOrWhiteSpace(traceParent))
        {
            return default;
        }

        var traceState = ReadHeader(properties.Headers, TraceState);
        return ActivityContext.TryParse(traceParent, traceState, true, out var context)
            ? context
            : default;
    }

    private static string? ReadHeader(IDictionary<string, object>? headers, string key)
    {
        if (headers is null || !headers.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            ReadOnlyMemory<byte> memory => Encoding.UTF8.GetString(memory.Span),
            string text => text,
            _ => value.ToString()
        };
    }
}
