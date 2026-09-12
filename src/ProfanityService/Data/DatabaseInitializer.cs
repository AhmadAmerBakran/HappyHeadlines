using Npgsql;

namespace ProfanityService.Data;

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
            .CreateLogger("ProfanityDatabaseInitializer");

        var connectionString = configuration.GetConnectionString("Profanity")
            ?? throw new InvalidOperationException("Connection string 'Profanity' is missing.");

        const string sql = """
            CREATE TABLE IF NOT EXISTS profanity_words (
                id SERIAL PRIMARY KEY,
                word VARCHAR(100) NOT NULL UNIQUE
            );

            INSERT INTO profanity_words (word)
            VALUES ('damn'), ('crap'), ('shit'), ('fuck')
            ON CONFLICT (word) DO NOTHING;
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
                    "Profanity database is not ready yet. Retrying ({Attempt}/{MaxAttempts}).",
                    attempt,
                    MaxAttempts);

                await Task.Delay(RetryDelay, cancellationToken);
            }
        }

        throw new InvalidOperationException("Could not initialise the profanity database.");
    }
}
