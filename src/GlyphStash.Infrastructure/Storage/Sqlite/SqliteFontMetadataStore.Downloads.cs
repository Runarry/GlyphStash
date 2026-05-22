using GlyphStash.Domain.Fonts;

namespace GlyphStash.Infrastructure.Storage.Sqlite;

public sealed partial class SqliteFontMetadataStore
{
    public async Task AddDownloadRecordAsync(DownloadRecord record, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(
            connection,
            null,
            """
            INSERT INTO download_records(provider_id, remote_id, family_name, variant, download_url, source_url, license_text, local_file_path, downloaded_at)
            VALUES($providerId, $remoteId, $familyName, $variant, $downloadUrl, $sourceUrl, $licenseText, $localFilePath, $downloadedAt);
            """,
            [
                new("$providerId", record.ProviderId),
                new("$remoteId", record.RemoteId),
                new("$familyName", record.FamilyName),
                new("$variant", record.Variant),
                new("$downloadUrl", record.DownloadUrl),
                new("$sourceUrl", record.SourceUrl),
                new("$licenseText", record.LicenseText),
                new("$localFilePath", record.LocalFilePath),
                new("$downloadedAt", record.DownloadedAt.ToString("O"))
            ],
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DownloadRecord>> GetRecentDownloadRecordsAsync(int limit, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT provider_id, remote_id, family_name, variant, download_url, source_url, license_text, local_file_path, downloaded_at
            FROM download_records
            ORDER BY id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", Math.Max(1, limit));
        var result = new List<DownloadRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(new DownloadRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                DateTimeOffset.TryParse(reader.GetString(8), out var downloadedAt) ? downloadedAt : DateTimeOffset.UnixEpoch));
        }

        return result;
    }
}
