namespace ArticleService.Data;

public static class ArticleScopes
{
    public static readonly string[] All =
    [
        "africa",
        "antarctica",
        "asia",
        "australia",
        "europe",
        "north-america",
        "south-america",
        "global"
    ];

    public static bool TryNormalize(string? value, out string scope)
    {
        scope = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim().ToLowerInvariant()
            .Replace('_', '-')
            .Replace(' ', '-');

        candidate = candidate switch
        {
            "northamerica" => "north-america",
            "southamerica" => "south-america",
            "oceania" => "australia",
            _ => candidate
        };

        if (!All.Contains(candidate, StringComparer.Ordinal))
        {
            return false;
        }

        scope = candidate;
        return true;
    }
}
