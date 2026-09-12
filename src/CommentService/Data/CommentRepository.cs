using CommentService.Models;
using Npgsql;

namespace CommentService.Data;

public sealed class CommentRepository(IConfiguration configuration) : ICommentRepository
{
    private readonly string _connectionString =
        configuration.GetConnectionString("Comments")
        ?? throw new InvalidOperationException("Connection string 'Comments' is missing.");

    public async Task AddAsync(Comment comment, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO comments
                (id, article_id, author, content, moderation_status, created_at_utc)
            VALUES
                (@id, @articleId, @author, @content, @moderationStatus, @createdAtUtc);
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", comment.Id);
        command.Parameters.AddWithValue("articleId", comment.ArticleId);
        command.Parameters.AddWithValue("author", comment.Author);
        command.Parameters.AddWithValue("content", comment.Content);
        command.Parameters.AddWithValue("moderationStatus", comment.ModerationStatus);
        command.Parameters.AddWithValue("createdAtUtc", comment.CreatedAtUtc);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<Comment?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, article_id, author, content, moderation_status, created_at_utc
            FROM comments
            WHERE id = @id;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapComment(reader);
    }

    public async Task<IReadOnlyList<Comment>> GetPublishedByArticleAsync(
        Guid articleId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, article_id, author, content, moderation_status, created_at_utc
            FROM comments
            WHERE article_id = @articleId
              AND moderation_status = 'published'
            ORDER BY created_at_utc ASC;
            """;

        var comments = new List<Comment>();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("articleId", articleId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            comments.Add(MapComment(reader));
        }

        return comments;
    }

    public async Task MarkPublishedAsync(
        Guid id,
        string filteredContent,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE comments
            SET content = @content,
                moderation_status = 'published'
            WHERE id = @id;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("content", filteredContent);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static Comment MapComment(NpgsqlDataReader reader)
    {
        return new Comment
        {
            Id = reader.GetGuid(0),
            ArticleId = reader.GetGuid(1),
            Author = reader.GetString(2),
            Content = reader.GetString(3),
            ModerationStatus = reader.GetString(4),
            CreatedAtUtc = reader.GetDateTime(5)
        };
    }
}
