using GlyphStash.Domain.Fonts;
using Microsoft.Data.Sqlite;

namespace GlyphStash.Infrastructure.Storage.Sqlite;

public sealed partial class SqliteFontMetadataStore
{
    public async Task<IReadOnlyList<FontCollectionRecord>> GetCollectionsAsync(CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var records = new Dictionary<string, List<string>>(StringComparer.CurrentCultureIgnoreCase);
        var metadata = new Dictionary<string, (DateTimeOffset? ExportedAt, int Temporary, int Unknown)>(StringComparer.CurrentCultureIgnoreCase);
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.name, c.last_exported_at, cf.family_name,
                   ff.activation_state, ff.license_status
            FROM collections c
            LEFT JOIN collection_fonts cf ON cf.collection_name = c.name
            LEFT JOIN font_families ff ON ff.family_name = cf.family_name
            ORDER BY c.name COLLATE NOCASE, cf.family_name COLLATE NOCASE;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var name = reader.GetString(0);
            if (!records.TryGetValue(name, out var families))
            {
                families = [];
                records.Add(name, families);
                metadata[name] = (
                    reader.IsDBNull(1) ? null : ParseLastSeenAt(reader.GetString(1)),
                    0,
                    0);
            }

            if (!reader.IsDBNull(2))
            {
                families.Add(reader.GetString(2));
                var current = metadata[name];
                metadata[name] = (
                    current.ExportedAt,
                    current.Temporary + (reader.IsDBNull(3) || (FontActivationState)reader.GetInt32(3) != FontActivationState.TemporarilyEnabled ? 0 : 1),
                    current.Unknown + (reader.IsDBNull(4) || (LicenseStatus)reader.GetInt32(4) == LicenseStatus.Known ? 0 : 1));
            }
        }

        return records
            .Select(pair =>
            {
                var meta = metadata[pair.Key];
                return new FontCollectionRecord(pair.Key, pair.Value, meta.Temporary, meta.Unknown, meta.ExportedAt);
            })
            .ToList();
    }

    public async Task CreateCollectionAsync(string name, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await UpsertCollectionAsync(connection, null, name, cancellationToken).ConfigureAwait(false);
    }

    public async Task RenameCollectionAsync(string oldName, string newName, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var dbTransaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)dbTransaction;
        await UpsertCollectionAsync(connection, transaction, newName, cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT OR IGNORE INTO collection_fonts(collection_name, family_name)
            SELECT $newName, family_name
            FROM collection_fonts
            WHERE collection_name = $oldName;
            """,
            [new("$newName", newName.Trim()), new("$oldName", oldName.Trim())],
            cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, transaction, "DELETE FROM collection_fonts WHERE collection_name = $oldName;", [new("$oldName", oldName.Trim())], cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, transaction, "DELETE FROM collections WHERE name = $oldName;", [new("$oldName", oldName.Trim())], cancellationToken).ConfigureAwait(false);
        await RebuildFamilyCollectionSummariesAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteCollectionAsync(string name, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var dbTransaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)dbTransaction;
        await ExecuteAsync(connection, transaction, "DELETE FROM collection_fonts WHERE collection_name = $name;", [new("$name", name.Trim())], cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, transaction, "DELETE FROM collections WHERE name = $name;", [new("$name", name.Trim())], cancellationToken).ConfigureAwait(false);
        await RebuildFamilyCollectionSummariesAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddFontToCollectionAsync(string collectionName, string familyName, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var dbTransaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)dbTransaction;
        await UpsertCollectionAsync(connection, transaction, collectionName, cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, transaction, "INSERT OR IGNORE INTO collection_fonts(collection_name, family_name) VALUES($collectionName, $familyName);", [new("$collectionName", collectionName.Trim()), new("$familyName", familyName)], cancellationToken).ConfigureAwait(false);
        await RebuildFamilyCollectionSummariesAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveFontFromCollectionAsync(string collectionName, string familyName, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var dbTransaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)dbTransaction;
        await ExecuteAsync(connection, transaction, "DELETE FROM collection_fonts WHERE collection_name = $collectionName AND family_name = $familyName;", [new("$collectionName", collectionName.Trim()), new("$familyName", familyName)], cancellationToken).ConfigureAwait(false);
        await RebuildFamilyCollectionSummariesAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
