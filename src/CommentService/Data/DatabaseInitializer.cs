using Npgsql;

namespace CommentService.Data;

public static class DatabaseInitializer
{
    private const int MaxAttempts = 30;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    public static async Task InitialiseAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("CommentDatabaseInitializer");

        var connectionString = configuration.GetConnectionString("Comments")
            ?? throw new InvalidOperationException("Connection string 'Comments' is missing.");

        const string sql = """
            CREATE TABLE IF NOT EXISTS comments (
                id UUID PRIMARY KEY,
                article_id UUID NOT NULL,
                author VARCHAR(100) NOT NULL,
                content TEXT NOT NULL,
                moderation_status VARCHAR(20) NOT NULL,
                created_at_utc TIMESTAMPTZ NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_comments_article_created
            ON comments (article_id, created_at_utc ASC);

            CREATE INDEX IF NOT EXISTS idx_comments_moderation_status
            ON comments (moderation_status);
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
                logger.LogWarning(
                    ex,
                    "Comment database is not ready yet. Retrying ({Attempt}/{MaxAttempts}).",
                    attempt,
                    MaxAttempts);

                await Task.Delay(RetryDelay, cancellationToken);
            }
        }

        throw new InvalidOperationException("Could not initialise the comment database.");
    }
}
