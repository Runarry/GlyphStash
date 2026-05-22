using GlyphStash.Domain.Fonts;
using Microsoft.Data.Sqlite;

namespace GlyphStash.Infrastructure.Storage.Sqlite;

public sealed partial class SqliteFontMetadataStore
{
    public async Task SetFavoriteAsync(string familyName, bool isFavorite, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = "UPDATE font_families SET is_favorite = $favorite, updated_at = datetime('now') WHERE family_name = $familyName;";
        command.Parameters.AddWithValue("$favorite", isFavorite ? 1 : 0);
        command.Parameters.AddWithValue("$familyName", familyName);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SetTagsAsync(string familyName, IReadOnlyList<string> tags, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var dbTransaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)dbTransaction;

        await ReplaceFamilyTagsAsync(connection, transaction, familyName, tags, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SetCollectionsAsync(string familyName, IReadOnlyList<string> collections, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var dbTransaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)dbTransaction;

        await ReplaceFamilyCollectionsAsync(connection, transaction, familyName, collections, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpsertManagedFontAsync(ManagedFontRecord managedFont, FontFamilyRecord family, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var dbTransaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)dbTransaction;

        var file = family.Faces.First().File;
        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO font_files(path, format, sha256, source_kind, last_seen_at)
            VALUES($path, $format, $sha256, $sourceKind, $lastSeenAt)
            ON CONFLICT(path) DO UPDATE SET
                format = excluded.format,
                sha256 = excluded.sha256,
                source_kind = excluded.source_kind,
                last_seen_at = excluded.last_seen_at;
            """,
            [
                new("$path", file.Path),
                new("$format", file.Format),
                new("$sha256", (object?)file.Sha256 ?? DBNull.Value),
                new("$sourceKind", (int)file.SourceKind),
                new("$lastSeenAt", file.LastSeenAt.ToString("O"))
            ],
            cancellationToken).ConfigureAwait(false);

        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO font_families(family_name, source_kind, activation_state, license_status, license_text, tags, collections, is_favorite, updated_at)
            VALUES($familyName, $sourceKind, $activationState, $licenseStatus, $licenseText, $tags, $collections, $favorite, datetime('now'))
            ON CONFLICT(family_name) DO UPDATE SET
                source_kind = excluded.source_kind,
                activation_state = excluded.activation_state,
                license_status = excluded.license_status,
                license_text = excluded.license_text,
                tags = excluded.tags,
                collections = excluded.collections,
                updated_at = excluded.updated_at;
            """,
            [
                new("$familyName", family.FamilyName),
                new("$sourceKind", (int)family.SourceKind),
                new("$activationState", (int)family.ActivationState),
                new("$licenseStatus", (int)family.LicenseStatus),
                new("$licenseText", family.LicenseText),
                new("$tags", JoinList(family.Tags)),
                new("$collections", JoinList(family.Collections)),
                new("$favorite", family.IsFavorite ? 1 : 0)
            ],
            cancellationToken).ConfigureAwait(false);

        foreach (var face in family.Faces)
        {
            await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO font_faces(family_name, subfamily_name, full_name, post_script_name, weight, width, slant, file_path)
                VALUES($familyName, $subfamilyName, $fullName, $postScriptName, $weight, $width, $slant, $filePath)
                ON CONFLICT(family_name, subfamily_name, file_path) DO UPDATE SET
                    full_name = excluded.full_name,
                    post_script_name = excluded.post_script_name,
                    weight = excluded.weight,
                    width = excluded.width,
                    slant = excluded.slant;
                """,
                [
                    new("$familyName", family.FamilyName),
                    new("$subfamilyName", face.SubfamilyName),
                    new("$fullName", face.FullName),
                    new("$postScriptName", face.PostScriptName),
                    new("$weight", face.Weight),
                    new("$width", face.Width),
                    new("$slant", face.Slant),
                    new("$filePath", face.File.Path)
                ],
                cancellationToken).ConfigureAwait(false);
        }

        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO managed_fonts(family_name, managed_file_path, format, sha256, installed_file_path, activation_state, imported_at, installed_at)
            VALUES($familyName, $managedFilePath, $format, $sha256, $installedFilePath, $activationState, $importedAt, $installedAt)
            ON CONFLICT(managed_file_path) DO UPDATE SET
                family_name = excluded.family_name,
                format = excluded.format,
                sha256 = excluded.sha256,
                installed_file_path = excluded.installed_file_path,
                activation_state = excluded.activation_state,
                installed_at = excluded.installed_at;
            """,
            [
                new("$familyName", managedFont.FamilyName),
                new("$managedFilePath", managedFont.ManagedFilePath),
                new("$format", managedFont.Format),
                new("$sha256", (object?)managedFont.Sha256 ?? DBNull.Value),
                new("$installedFilePath", (object?)managedFont.InstalledFilePath ?? DBNull.Value),
                new("$activationState", (int)managedFont.ActivationState),
                new("$importedAt", managedFont.ImportedAt.ToString("O")),
                new("$installedAt", (object?)managedFont.InstalledAt?.ToString("O") ?? DBNull.Value)
            ],
            cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        await SetTagsAsync(family.FamilyName, family.Tags, cancellationToken).ConfigureAwait(false);
        await SetCollectionsAsync(family.FamilyName, family.Collections, cancellationToken).ConfigureAwait(false);
    }
}
