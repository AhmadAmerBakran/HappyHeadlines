using HappyHeadlines.Observability;
using Npgsql;

namespace DraftService.Data;

public static class DatabaseInitializer
{
    private const int MaxAttempts = 30;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    public static async Task InitialiseAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DraftDatabaseInitializer");
        var connectionString = configuration.GetConnectionString("Drafts")
            ?? throw new InvalidOperationException("ConnectionStrings:Drafts is missing.");

        const string sql = """
            CREATE TABLE IF NOT EXISTS drafts (
                id UUID PRIMARY KEY,
                title VARCHAR(200) NOT NULL,
                content TEXT NOT NULL,
                author VARCHAR(100) NOT NULL,
                created_at_utc TIMESTAMPTZ NOT NULL,
                updated_at_utc TIMESTAMPTZ NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_drafts_updated_at_utc
            ON drafts (updated_at_utc DESC);
            """;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                using var activity = HappyHeadlinesDiagnostics.StartActivity("draft-database.initialise");
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);
                await using var command = new NpgsqlCommand(sql, connection);
                await command.ExecuteNonQueryAsync(cancellationToken);
                logger.LogInformation("Draft database is ready");
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts && !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(
                    ex,
                    "Draft database is not ready. Retrying ({Attempt}/{MaxAttempts})",
                    attempt,
                    MaxAttempts);

                await Task.Delay(RetryDelay, cancellationToken);
            }
        }

        throw new InvalidOperationException("Could not initialise the draft database.");
    }
}
