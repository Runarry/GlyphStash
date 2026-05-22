using GlyphStash.Domain.Fonts;
using Microsoft.Data.Sqlite;

namespace GlyphStash.Infrastructure.Storage.Sqlite;

public sealed partial class SqliteFontMetadataStore
{
    public async Task SaveFontIndexAsync(IReadOnlyList<FontFamilyRecord> fonts, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var dbTransaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)dbTransaction;
        var tableColumns = await ReadFontIndexColumnsAsync(connection, cancellationToken).ConfigureAwait(false);
        var preservedFamilies = await ReadPreservedFamilyMetadataAsync(connection, tableColumns, cancellationToken, transaction).ConfigureAwait(false);
        await PruneManagedFontRecordsAsync(connection, transaction, GetCurrentManagedFilePaths(fonts), cancellationToken).ConfigureAwait(false);

        await ExecuteAsync(connection, transaction, "DELETE FROM font_faces;", cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, transaction, "DELETE FROM font_families;", cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, transaction, "DELETE FROM font_files;", cancellationToken).ConfigureAwait(false);

        foreach (var family in fonts.OrderBy(font => font.FamilyName, StringComparer.CurrentCultureIgnoreCase))
        {
            foreach (var file in family.Faces.Select(face => face.File).DistinctBy(file => file.Path))
            {
                if (string.IsNullOrWhiteSpace(file.Path))
                {
                    throw new InvalidOperationException($"Font '{family.FamilyName}' has an empty file identity and cannot be cached.");
                }

                var fileCommand = connection.CreateCommand();
                fileCommand.Transaction = transaction;
                fileCommand.CommandText = """
                    INSERT INTO font_files(path, format, sha256, source_kind, last_seen_at)
                    VALUES($path, $format, $sha256, $sourceKind, $lastSeenAt)
                    ON CONFLICT(path) DO UPDATE SET
                        format = excluded.format,
                        sha256 = excluded.sha256,
                        source_kind = excluded.source_kind,
                        last_seen_at = excluded.last_seen_at;
                    """;
                fileCommand.Parameters.AddWithValue("$path", file.Path);
                fileCommand.Parameters.AddWithValue("$format", file.Format);
                fileCommand.Parameters.AddWithValue("$sha256", (object?)file.Sha256 ?? DBNull.Value);
                fileCommand.Parameters.AddWithValue("$sourceKind", (int)file.SourceKind);
                fileCommand.Parameters.AddWithValue("$lastSeenAt", file.LastSeenAt.ToString("O"));
                await fileCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            var familyCommand = connection.CreateCommand();
            familyCommand.Transaction = transaction;
            familyCommand.CommandText = """
                INSERT INTO font_families(family_name, source_kind, activation_state, license_status, license_text, tags, collections, is_favorite, updated_at)
                VALUES($familyName, $sourceKind, $activationState, $licenseStatus, $licenseText, $tags, $collections, $isFavorite, datetime('now'))
                ON CONFLICT(family_name) DO UPDATE SET
                    source_kind = excluded.source_kind,
                    activation_state = excluded.activation_state,
                    license_status = excluded.license_status,
                    license_text = excluded.license_text,
                    tags = excluded.tags,
                    collections = excluded.collections,
                    is_favorite = excluded.is_favorite,
                    updated_at = excluded.updated_at;
                """;
            preservedFamilies.TryGetValue(family.FamilyName, out var preservedFamily);
            var tags = MergeNames(preservedFamily is null ? family.Tags : preservedFamily.Tags, InferAutomaticTags(family));
            var collections = preservedFamily is null ? family.Collections : preservedFamily.Collections;
            var isFavorite = preservedFamily is null ? family.IsFavorite : preservedFamily.IsFavorite;
            familyCommand.Parameters.AddWithValue("$familyName", family.FamilyName);
            familyCommand.Parameters.AddWithValue("$sourceKind", (int)family.SourceKind);
            familyCommand.Parameters.AddWithValue("$activationState", (int)family.ActivationState);
            familyCommand.Parameters.AddWithValue("$licenseStatus", (int)family.LicenseStatus);
            familyCommand.Parameters.AddWithValue("$licenseText", family.LicenseText);
            familyCommand.Parameters.AddWithValue("$tags", JoinList(tags));
            familyCommand.Parameters.AddWithValue("$collections", JoinList(collections));
            familyCommand.Parameters.AddWithValue("$isFavorite", isFavorite ? 1 : 0);
            await familyCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            await ReplaceFamilyTagsAsync(connection, transaction, family.FamilyName, tags, cancellationToken).ConfigureAwait(false);
            await ReplaceFamilyCollectionsAsync(connection, transaction, family.FamilyName, collections, cancellationToken).ConfigureAwait(false);

            foreach (var face in family.Faces)
            {
                if (string.IsNullOrWhiteSpace(face.File.Path))
                {
                    throw new InvalidOperationException($"Font face '{face.FullName}' has an empty file identity and cannot be cached.");
                }

                var faceCommand = connection.CreateCommand();
                faceCommand.Transaction = transaction;
                faceCommand.CommandText = """
                    INSERT INTO font_faces(family_name, subfamily_name, full_name, post_script_name, weight, width, slant, file_path)
                    VALUES($familyName, $subfamilyName, $fullName, $postScriptName, $weight, $width, $slant, $filePath)
                    ON CONFLICT(family_name, subfamily_name, file_path) DO UPDATE SET
                        full_name = excluded.full_name,
                        post_script_name = excluded.post_script_name,
                        weight = excluded.weight,
                        width = excluded.width,
                        slant = excluded.slant;
                    """;
                faceCommand.Parameters.AddWithValue("$familyName", family.FamilyName);
                faceCommand.Parameters.AddWithValue("$subfamilyName", face.SubfamilyName);
                faceCommand.Parameters.AddWithValue("$fullName", face.FullName);
                faceCommand.Parameters.AddWithValue("$postScriptName", face.PostScriptName);
                faceCommand.Parameters.AddWithValue("$weight", face.Weight);
                faceCommand.Parameters.AddWithValue("$width", face.Width);
                faceCommand.Parameters.AddWithValue("$slant", face.Slant);
                faceCommand.Parameters.AddWithValue("$filePath", face.File.Path);
                await faceCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<FontFamilyRecord>> SearchAsync(FontSearchQuery query, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ff.family_name, ff.source_kind, ff.activation_state, ff.license_status, ff.license_text,
                   ff.tags, ff.collections, ff.is_favorite,
                   fc.subfamily_name, fc.full_name, fc.post_script_name, fc.weight, fc.width, fc.slant,
                   files.path, files.format, files.sha256, files.source_kind, files.last_seen_at
            FROM font_families ff
            JOIN font_faces fc ON fc.family_name = ff.family_name
            JOIN font_files files ON files.path = fc.file_path
            ORDER BY ff.family_name COLLATE NOCASE, fc.weight, fc.subfamily_name;
            """;

        var families = new Dictionary<string, MutableFamily>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var familyName = reader.GetString(0);
            if (!families.TryGetValue(familyName, out var family))
            {
                family = new MutableFamily(
                    familyName,
                    (FontSourceKind)reader.GetInt32(1),
                    (FontActivationState)reader.GetInt32(2),
                    (LicenseStatus)reader.GetInt32(3),
                    reader.GetString(4),
                    SplitList(reader.GetString(5)),
                    SplitList(reader.GetString(6)),
                    reader.GetInt32(7) == 1);
                families.Add(familyName, family);
            }

            var file = new FontFileRecord(
                reader.GetString(14),
                reader.GetString(15),
                reader.IsDBNull(16) ? null : reader.GetString(16),
                (FontSourceKind)reader.GetInt32(17),
                ParseLastSeenAt(reader.GetString(18)));
            family.Faces.Add(new FontFaceRecord(
                familyName,
                reader.GetString(8),
                reader.GetString(9),
                reader.GetString(10),
                reader.GetInt32(11),
                reader.GetString(12),
                reader.GetString(13),
                file));
        }

        return families.Values
            .Select(family => family.ToRecord())
            .Where(font => Matches(font, query))
            .ToList();
    }
}
