using ArticleService.Models;
using Npgsql;

namespace ArticleService.Data;

public sealed class ArticleRepository(IArticleShardResolver shardResolver) : IArticleRepository
{
    public async Task<Article> CreateAsync(Article article, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(shardResolver.GetConnectionString(article.Scope));
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            INSERT INTO articles (id, title, content, source, scope, created_at_utc, updated_at_utc)
            VALUES (@id, @title, @content, @source, @scope, @createdAtUtc, @updatedAtUtc);
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", article.Id);
        command.Parameters.AddWithValue("title", article.Title);
        command.Parameters.AddWithValue("content", article.Content);
        command.Parameters.AddWithValue("source", (object?)article.Source ?? DBNull.Value);
        command.Parameters.AddWithValue("scope", article.Scope);
        command.Parameters.AddWithValue("createdAtUtc", article.CreatedAtUtc);
        command.Parameters.AddWithValue("updatedAtUtc", article.UpdatedAtUtc);

        await command.ExecuteNonQueryAsync(cancellationToken);
        return article;
    }

    public async Task<Article?> GetByIdAsync(string scope, Guid id, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(shardResolver.GetConnectionString(scope));
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT id, title, content, source, scope, created_at_utc, updated_at_utc
            FROM articles
            WHERE id = @id
            LIMIT 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    public async Task<IReadOnlyList<Article>> GetAllAsync(string scope, int limit, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(shardResolver.GetConnectionString(scope));
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT id, title, content, source, scope, created_at_utc, updated_at_utc
            FROM articles
            ORDER BY created_at_utc DESC
            LIMIT @limit;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("limit", limit);

        var articles = new List<Article>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            articles.Add(Map(reader));
        }

        return articles;
    }

    public async Task<Article?> UpdateAsync(
        string scope,
        Guid id,
        string title,
        string content,
        string? source,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(shardResolver.GetConnectionString(scope));
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            UPDATE articles
            SET title = @title,
                content = @content,
                source = @source,
                updated_at_utc = @updatedAtUtc
            WHERE id = @id
            RETURNING id, title, content, source, scope, created_at_utc, updated_at_utc;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("title", title);
        command.Parameters.AddWithValue("content", content);
        command.Parameters.AddWithValue("source", (object?)source ?? DBNull.Value);
        command.Parameters.AddWithValue("updatedAtUtc", DateTime.UtcNow);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    public async Task<bool> DeleteAsync(string scope, Guid id, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(shardResolver.GetConnectionString(scope));
        await connection.OpenAsync(cancellationToken);

        const string sql = "DELETE FROM articles WHERE id = @id;";
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);

        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private static Article Map(NpgsqlDataReader reader)
    {
        return new Article
        {
            Id = reader.GetGuid(0),
            Title = reader.GetString(1),
            Content = reader.GetString(2),
            Source = reader.IsDBNull(3) ? null : reader.GetString(3),
            Scope = reader.GetString(4),
            CreatedAtUtc = reader.GetDateTime(5),
            UpdatedAtUtc = reader.GetDateTime(6)
        };
    }
}
