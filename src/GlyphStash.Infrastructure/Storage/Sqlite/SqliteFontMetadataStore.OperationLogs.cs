using GlyphStash.Domain.Fonts;

namespace GlyphStash.Infrastructure.Storage.Sqlite;

public sealed partial class SqliteFontMetadataStore
{
    public async Task AppendOperationAsync(OperationLogEntry entry, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(
            connection,
            null,
            """
            INSERT INTO operation_logs(timestamp, category, action, message, target, succeeded)
            VALUES($timestamp, $category, $action, $message, $target, $succeeded);
            """,
            [
                new("$timestamp", entry.Timestamp.ToString("O")),
                new("$category", entry.Category),
                new("$action", entry.Action),
                new("$message", entry.Message),
                new("$target", (object?)entry.Target ?? DBNull.Value),
                new("$succeeded", entry.Succeeded ? 1 : 0)
            ],
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OperationLogEntry>> GetRecentOperationsAsync(int limit, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT timestamp, category, action, message, target, succeeded
            FROM operation_logs
            ORDER BY id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", Math.Max(1, limit));
        var result = new List<OperationLogEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(new OperationLogEntry(
                ParseLastSeenAt(reader.GetString(0)),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetInt32(5) == 1));
        }

        return result;
    }
}
