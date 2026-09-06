namespace ArticleService.Data;

public sealed class ArticleShardResolver : IArticleShardResolver
{
    private readonly IReadOnlyDictionary<string, string> _connections;

    public ArticleShardResolver(IConfiguration configuration)
    {
        _connections = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["africa"] = RequiredConnectionString(configuration, "Africa"),
            ["antarctica"] = RequiredConnectionString(configuration, "Antarctica"),
            ["asia"] = RequiredConnectionString(configuration, "Asia"),
            ["australia"] = RequiredConnectionString(configuration, "Australia"),
            ["europe"] = RequiredConnectionString(configuration, "Europe"),
            ["north-america"] = RequiredConnectionString(configuration, "NorthAmerica"),
            ["south-america"] = RequiredConnectionString(configuration, "SouthAmerica"),
            ["global"] = RequiredConnectionString(configuration, "Global")
        };
    }

    public string GetConnectionString(string scope)
    {
        if (!ArticleScopes.TryNormalize(scope, out var normalized) ||
            !_connections.TryGetValue(normalized, out var connectionString))
        {
            throw new ArgumentException($"Unknown article scope '{scope}'.", nameof(scope));
        }

        return connectionString;
    }

    public IReadOnlyCollection<string> GetAllConnectionStrings() => _connections.Values.ToArray();

    private static string RequiredConnectionString(IConfiguration configuration, string name)
    {
        return configuration.GetConnectionString(name)
            ?? throw new InvalidOperationException($"Connection string '{name}' is missing.");
    }
}
