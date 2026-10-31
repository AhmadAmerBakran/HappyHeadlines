using Npgsql;

namespace SubscriberService.Data;

public sealed class SubscriberRepository(IConfiguration configuration)
{
    private readonly string _connectionString = configuration.GetConnectionString("Subscribers")
        ?? throw new InvalidOperationException("ConnectionStrings:Subscribers is missing.");

    public async Task InitialiseAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS subscribers (
                id UUID PRIMARY KEY,
                email VARCHAR(254) NOT NULL UNIQUE,
                subscribed_at_utc TIMESTAMPTZ NOT NULL
            );

            CREATE TABLE IF NOT EXISTS subscriber_outbox (
                id UUID PRIMARY KEY,
                email VARCHAR(254) NOT NULL,
                subscribed_at_utc TIMESTAMPTZ NOT NULL,
                dispatched_at_utc TIMESTAMPTZ
            );

            CREATE INDEX IF NOT EXISTS idx_subscriber_outbox_pending
                ON subscriber_outbox (subscribed_at_utc)
                WHERE dispatched_at_utc IS NULL;

            CREATE TABLE IF NOT EXISTS subscriber_rollout (
                id INTEGER PRIMARY KEY CHECK (id = 1),
                enabled BOOLEAN NOT NULL DEFAULT FALSE,
                percentage INTEGER NOT NULL DEFAULT 0 CHECK (percentage BETWEEN 0 AND 100)
            );

            INSERT INTO subscriber_rollout (id, enabled, percentage)
            VALUES (1, FALSE, 0)
            ON CONFLICT (id) DO NOTHING;
            """;

        for (var attempt = 1; attempt <= 30; attempt++)
        {
            try
            {
                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);
                await using var command = new NpgsqlCommand(sql, connection);
                await command.ExecuteNonQueryAsync(cancellationToken);
                return;
            }
            catch (Exception) when (attempt < 30 && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }

        throw new InvalidOperationException("The subscriber database is unavailable.");
    }

    public async Task<ReleaseRollout> GetRolloutAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT enabled, percentage FROM subscriber_rollout WHERE id = 1;";
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new ReleaseRollout(false, 0);
        }

        return new ReleaseRollout(reader.GetBoolean(0), reader.GetInt32(1));
    }

    public async Task UpdateRolloutAsync(ReleaseRollout rollout, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE subscriber_rollout
            SET enabled = @enabled, percentage = @percentage
            WHERE id = 1;
            """;
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("enabled", rollout.Enabled);
        command.Parameters.AddWithValue("percentage", rollout.Percentage);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<(bool Created, Guid Id)> SubscribeAsync(string email, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        var subscribedAt = DateTime.UtcNow;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string insert = """
            INSERT INTO subscribers (id, email, subscribed_at_utc)
            VALUES (@id, @email, @subscribedAt)
            ON CONFLICT (email) DO NOTHING
            RETURNING id;
            """;
        await using (var command = new NpgsqlCommand(insert, connection, transaction))
        {
            command.Parameters.AddWithValue("id", id);
            command.Parameters.AddWithValue("email", email);
            command.Parameters.AddWithValue("subscribedAt", subscribedAt);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            if (result is null)
            {
                await transaction.CommitAsync(cancellationToken);
                return (false, Guid.Empty);
            }
        }

        const string addEvent = """
            INSERT INTO subscriber_outbox (id, email, subscribed_at_utc)
            VALUES (@id, @email, @subscribedAt);
            """;
        await using (var command = new NpgsqlCommand(addEvent, connection, transaction))
        {
            command.Parameters.AddWithValue("id", id);
            command.Parameters.AddWithValue("email", email);
            command.Parameters.AddWithValue("subscribedAt", subscribedAt);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return (true, id);
    }

    public async Task<bool> UnsubscribeAsync(string email, CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM subscribers WHERE email = @email;";
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("email", email);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<IReadOnlyList<string>> ListEmailsAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT email FROM subscribers ORDER BY subscribed_at_utc;";
        var emails = new List<string>();
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            emails.Add(reader.GetString(0));
        }

        return emails;
    }

    public async Task<IReadOnlyList<SubscriberCreated>> PendingEventsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, email, subscribed_at_utc
            FROM subscriber_outbox
            WHERE dispatched_at_utc IS NULL
            ORDER BY subscribed_at_utc
            LIMIT 20;
            """;
        var events = new List<SubscriberCreated>();
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            events.Add(new SubscriberCreated(reader.GetGuid(0), reader.GetString(1), reader.GetDateTime(2)));
        }

        return events;
    }

    public async Task MarkDispatchedAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE subscriber_outbox SET dispatched_at_utc = now()
            WHERE id = @id AND dispatched_at_utc IS NULL;
            """;
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
