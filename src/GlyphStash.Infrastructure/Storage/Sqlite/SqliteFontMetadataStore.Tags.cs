using GlyphStash.Domain.Fonts;
using Microsoft.Data.Sqlite;

namespace GlyphStash.Infrastructure.Storage.Sqlite;

public sealed partial class SqliteFontMetadataStore
{
    public async Task<IReadOnlyList<TagRecord>> GetTagsAsync(CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT t.name, COUNT(ft.family_name)
            FROM tags t
            LEFT JOIN font_tags ft ON ft.tag_name = t.name
            GROUP BY t.name
            ORDER BY t.name COLLATE NOCASE;
            """;
        var tags = new List<TagRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            tags.Add(new TagRecord(reader.GetString(0), Convert.ToInt32(reader.GetInt64(1))));
        }

        return tags;
    }

    public async Task CreateTagAsync(string name, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await UpsertTagAsync(connection, null, name, cancellationToken).ConfigureAwait(false);
    }

    public async Task RenameTagAsync(string oldName, string newName, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var dbTransaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)dbTransaction;

        await UpsertTagAsync(connection, transaction, newName, cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT OR IGNORE INTO font_tags(family_name, tag_name)
            SELECT family_name, $newName
            FROM font_tags
            WHERE tag_name = $oldName;
            """,
            [new("$newName", newName.Trim()), new("$oldName", oldName.Trim())],
            cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, transaction, "DELETE FROM font_tags WHERE tag_name = $oldName;", [new("$oldName", oldName.Trim())], cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, transaction, "DELETE FROM tags WHERE name = $oldName;", [new("$oldName", oldName.Trim())], cancellationToken).ConfigureAwait(false);
        await RebuildFamilyTagSummariesAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteTagAsync(string name, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var dbTransaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)dbTransaction;

        await ExecuteAsync(connection, transaction, "DELETE FROM font_tags WHERE tag_name = $name;", [new("$name", name.Trim())], cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, transaction, "DELETE FROM tags WHERE name = $name;", [new("$name", name.Trim())], cancellationToken).ConfigureAwait(false);
        await RebuildFamilyTagSummariesAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
