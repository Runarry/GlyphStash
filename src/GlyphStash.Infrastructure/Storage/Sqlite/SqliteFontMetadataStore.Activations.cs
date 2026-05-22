using GlyphStash.Domain.Fonts;

namespace GlyphStash.Infrastructure.Storage.Sqlite;

public sealed partial class SqliteFontMetadataStore
{
    public async Task<IReadOnlyList<ActivationRecord>> GetOwnedActivationsAsync(CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT font_path, owner_key, reference_count, platform_flags, activated_at, last_known_state, cleanup_status, sha256, scope
            FROM activation_records
            WHERE last_known_state = 'Active'
            ORDER BY activated_at;
            """;
        var result = new List<ActivationRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(new ActivationRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetInt32(3),
                ParseLastSeenAt(reader.GetString(4)),
                reader.GetString(5),
                reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.GetString(8)));
        }

        return result;
    }

    public async Task UpsertActivationAsync(ActivationRecord record, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(
            connection,
            null,
            """
            INSERT INTO activation_records(font_path, owner_key, reference_count, platform_flags, activated_at, last_known_state, cleanup_status, sha256, scope)
            VALUES($fontPath, $ownerKey, $referenceCount, $platformFlags, $activatedAt, $lastKnownState, $cleanupStatus, $sha256, $scope)
            ON CONFLICT(font_path, owner_key) DO UPDATE SET
                reference_count = excluded.reference_count,
                platform_flags = excluded.platform_flags,
                last_known_state = excluded.last_known_state,
                cleanup_status = excluded.cleanup_status,
                sha256 = excluded.sha256,
                scope = excluded.scope;
            """,
            [
                new("$fontPath", record.FontPath),
                new("$ownerKey", record.OwnerKey),
                new("$referenceCount", record.ReferenceCount),
                new("$platformFlags", record.PlatformFlags),
                new("$activatedAt", record.ActivatedAt.ToString("O")),
                new("$lastKnownState", record.LastKnownState),
                new("$cleanupStatus", record.CleanupStatus),
                new("$sha256", (object?)record.Sha256 ?? DBNull.Value),
                new("$scope", record.Scope)
            ],
            cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveActivationAsync(string fontPath, string ownerKey, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, null, "DELETE FROM activation_records WHERE font_path = $fontPath AND owner_key = $ownerKey;", [new("$fontPath", fontPath), new("$ownerKey", ownerKey)], cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkAllOwnedStaleAsync(CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, null, "UPDATE activation_records SET last_known_state = 'StaleAfterRestart', cleanup_status = 'RequiresUserVerification' WHERE last_known_state = 'Active';", [], cancellationToken).ConfigureAwait(false);
    }
}
