using Npgsql;

namespace ArticleService.Data;

public static class DatabaseInitializer
{
    private const int MaxAttempts = 30;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    public static async Task InitialiseAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IArticleShardResolver>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");

        var tasks = resolver.GetAllConnectionStrings()
            .Distinct(StringComparer.Ordinal)
            .Select(connectionString => InitialiseDatabaseAsync(connectionString, logger, cancellationToken));

        await Task.WhenAll(tasks);
    }

    private static async Task InitialiseDatabaseAsync(
        string connectionString,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT pg_advisory_lock(360036);

            CREATE TABLE IF NOT EXISTS articles (
                id UUID PRIMARY KEY,
                title VARCHAR(200) NOT NULL,
                content TEXT NOT NULL,
                source VARCHAR(500) NULL,
                scope VARCHAR(32) NOT NULL,
                created_at_utc TIMESTAMPTZ NOT NULL,
                updated_at_utc TIMESTAMPTZ NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_articles_created_at_utc
            ON articles (created_at_utc DESC);

            SELECT pg_advisory_unlock(360036);
            """;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);
                await using var command = new NpgsqlCommand(sql, connection);
                await command.ExecuteNonQueryAsync(cancellationToken);
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts && !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Database is not ready yet. Retrying ({Attempt}/{MaxAttempts}).", attempt, MaxAttempts);
                await Task.Delay(RetryDelay, cancellationToken);
            }
        }

        throw new InvalidOperationException("Could not initialise one of the article databases.");
    }
}
