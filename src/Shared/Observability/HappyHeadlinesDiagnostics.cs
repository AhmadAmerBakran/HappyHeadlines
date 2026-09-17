using System.Diagnostics;

namespace HappyHeadlines.Observability;

public static class HappyHeadlinesDiagnostics
{
    public const string ActivitySourceName = "HappyHeadlines";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    public static Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
        => ActivitySource.StartActivity(name, kind);
}
