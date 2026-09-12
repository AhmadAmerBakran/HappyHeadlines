using Npgsql;

namespace ProfanityService.Data;

public sealed class ProfanityRepository(IConfiguration configuration) : IProfanityRepository
{
    private readonly string _connectionString =
        configuration.GetConnectionString("Profanity")
        ?? throw new InvalidOperationException("Connection string 'Profanity' is missing.");

    public async Task<IReadOnlyList<string>> GetWordsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT word
            FROM profanity_words
            ORDER BY length(word) DESC, word ASC;
            """;

        var words = new List<string>();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            words.Add(reader.GetString(0));
        }

        return words;
    }
}
