using System.Diagnostics;
using DraftService.Models;
using HappyHeadlines.Observability;
using Npgsql;

namespace DraftService.Data;

public sealed class DraftRepository : IDraftRepository
{
    private readonly string _connectionString;
    private readonly ILogger<DraftRepository> _logger;

    public DraftRepository(IConfiguration configuration, ILogger<DraftRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("Drafts")
            ?? throw new InvalidOperationException("ConnectionStrings:Drafts is missing.");
        _logger = logger;
    }

    public async Task<IReadOnlyList<Draft>> GetAllAsync(CancellationToken cancellationToken)
    {
        using var activity = StartDatabaseActivity("select-all");
        _logger.LogInformation("Fetching drafts from the draft database");

        const string sql = """
            SELECT id, title, content, author, created_at_utc, updated_at_utc
            FROM drafts
            ORDER BY updated_at_utc DESC;
            """;

        var drafts = new List<Draft>();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            drafts.Add(MapDraft(reader));
        }

        _logger.LogInformation("Fetched {DraftCount} drafts", drafts.Count);
        activity?.SetTag("draft.count", drafts.Count);
        return drafts;
    }

    public async Task<Draft?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        using var activity = StartDatabaseActivity("select", id);

        const string sql = """
            SELECT id, title, content, author, created_at_utc, updated_at_utc
            FROM drafts
            WHERE id = @id;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapDraft(reader) : null;
    }

    public async Task CreateAsync(Draft draft, CancellationToken cancellationToken)
    {
        using var activity = StartDatabaseActivity("insert", draft.Id);
        _logger.LogInformation("Saving draft {DraftId} for {Author}", draft.Id, draft.Author);

        const string sql = """
            INSERT INTO drafts (id, title, content, author, created_at_utc, updated_at_utc)
            VALUES (@id, @title, @content, @author, @createdAtUtc, @updatedAtUtc);
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", draft.Id);
        command.Parameters.AddWithValue("title", draft.Title);
        command.Parameters.AddWithValue("content", draft.Content);
        command.Parameters.AddWithValue("author", draft.Author);
        command.Parameters.AddWithValue("createdAtUtc", draft.CreatedAtUtc);
        command.Parameters.AddWithValue("updatedAtUtc", draft.UpdatedAtUtc);

        await command.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation("Draft {DraftId} saved", draft.Id);
    }

    public async Task<bool> UpdateAsync(Draft draft, CancellationToken cancellationToken)
    {
        using var activity = StartDatabaseActivity("update", draft.Id);
        _logger.LogInformation("Updating draft {DraftId}", draft.Id);

        const string sql = """
            UPDATE drafts
            SET title = @title,
                content = @content,
                updated_at_utc = @updatedAtUtc
            WHERE id = @id;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", draft.Id);
        command.Parameters.AddWithValue("title", draft.Title);
        command.Parameters.AddWithValue("content", draft.Content);
        command.Parameters.AddWithValue("updatedAtUtc", draft.UpdatedAtUtc);

        var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);
        return affectedRows == 1;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        using var activity = StartDatabaseActivity("delete", id);
        _logger.LogInformation("Deleting draft {DraftId}", id);

        const string sql = "DELETE FROM drafts WHERE id = @id;";

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);

        var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);
        return affectedRows == 1;
    }

    private static Activity? StartDatabaseActivity(string operation, Guid? draftId = null)
    {
        var activity = HappyHeadlinesDiagnostics.StartActivity($"drafts.{operation}", ActivityKind.Client);
        activity?.SetTag("db.system", "postgresql");
        activity?.SetTag("db.operation", operation);

        if (draftId.HasValue)
        {
            activity?.SetTag("draft.id", draftId.Value.ToString());
        }

        return activity;
    }

    private static Draft MapDraft(NpgsqlDataReader reader)
    {
        return new Draft
        {
            Id = reader.GetGuid(0),
            Title = reader.GetString(1),
            Content = reader.GetString(2),
            Author = reader.GetString(3),
            CreatedAtUtc = reader.GetDateTime(4),
            UpdatedAtUtc = reader.GetDateTime(5)
        };
    }
}
