using GlyphStash.Application.Abstractions.Storage;
using GlyphStash.Domain.Fonts;
using Microsoft.Data.Sqlite;

namespace GlyphStash.Infrastructure.Storage.Sqlite;

public sealed class SqliteFontMetadataStore : IFontMetadataStore
{
    private const string SchemaVersionTable = "schema_version";
    private const string FontFilesTable = "font_files";
    private const string FontFamiliesTable = "font_families";
    private const string FontFacesTable = "font_faces";

    private static readonly CanonicalTableSchema SchemaVersionSchema = new(
        SchemaVersionTable,
        [
            new("version", "INTEGER", true, 1),
            new("applied_at", "TEXT", true, 0)
        ],
        """
        CREATE TABLE schema_version (
            version INTEGER NOT NULL PRIMARY KEY,
            applied_at TEXT NOT NULL
        );
        """);

    private static readonly CanonicalTableSchema FontFilesSchema = new(
        FontFilesTable,
        [
            new("path", "TEXT", true, 1),
            new("format", "TEXT", true, 0),
            new("sha256", "TEXT", false, 0),
            new("source_kind", "INTEGER", true, 0),
            new("last_seen_at", "TEXT", true, 0)
        ],
        """
        CREATE TABLE font_files (
            path TEXT NOT NULL PRIMARY KEY,
            format TEXT NOT NULL,
            sha256 TEXT NULL,
            source_kind INTEGER NOT NULL,
            last_seen_at TEXT NOT NULL
        );
        """);

    private static readonly CanonicalTableSchema FontFamiliesSchema = new(
        FontFamiliesTable,
        [
            new("family_name", "TEXT", true, 1),
            new("source_kind", "INTEGER", true, 0),
            new("activation_state", "INTEGER", true, 0),
            new("license_status", "INTEGER", true, 0),
            new("license_text", "TEXT", true, 0),
            new("tags", "TEXT", true, 0),
            new("collections", "TEXT", true, 0),
            new("is_favorite", "INTEGER", true, 0),
            new("updated_at", "TEXT", true, 0)
        ],
        """
        CREATE TABLE font_families (
            family_name TEXT NOT NULL PRIMARY KEY,
            source_kind INTEGER NOT NULL,
            activation_state INTEGER NOT NULL,
            license_status INTEGER NOT NULL,
            license_text TEXT NOT NULL,
            tags TEXT NOT NULL,
            collections TEXT NOT NULL,
            is_favorite INTEGER NOT NULL DEFAULT 0,
            updated_at TEXT NOT NULL
        );
        """);

    private static readonly CanonicalTableSchema FontFacesSchema = new(
        FontFacesTable,
        [
            new("id", "INTEGER", false, 1),
            new("family_name", "TEXT", true, 0),
            new("subfamily_name", "TEXT", true, 0),
            new("full_name", "TEXT", true, 0),
            new("post_script_name", "TEXT", true, 0),
            new("weight", "INTEGER", true, 0),
            new("width", "TEXT", true, 0),
            new("slant", "TEXT", true, 0),
            new("file_path", "TEXT", true, 0)
        ],
        """
        CREATE TABLE font_faces (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            family_name TEXT NOT NULL,
            subfamily_name TEXT NOT NULL,
            full_name TEXT NOT NULL,
            post_script_name TEXT NOT NULL,
            weight INTEGER NOT NULL,
            width TEXT NOT NULL,
            slant TEXT NOT NULL,
            file_path TEXT NOT NULL,
            UNIQUE(family_name, subfamily_name, file_path),
            FOREIGN KEY(family_name) REFERENCES font_families(family_name) ON DELETE CASCADE,
            FOREIGN KEY(file_path) REFERENCES font_files(path) ON DELETE CASCADE
        );
        """);

    private static readonly CanonicalTableSchema[] FontIndexSchemas =
    [
        FontFilesSchema,
        FontFamiliesSchema,
        FontFacesSchema
    ];

    private readonly string _databasePath;
    private bool _initialized;

    public SqliteFontMetadataStore(string databasePath)
    {
        _databasePath = databasePath;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await ExecuteAsync(connection, "PRAGMA journal_mode = WAL;", cancellationToken).ConfigureAwait(false);
        await EnsureSchemaVersionAsync(connection, cancellationToken).ConfigureAwait(false);
        await EnsureCanonicalFontIndexSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        _initialized = true;
    }

    public async Task SaveFontIndexAsync(IReadOnlyList<FontFamilyRecord> fonts, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var dbTransaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)dbTransaction;
        var tableColumns = await ReadFontIndexColumnsAsync(connection, cancellationToken).ConfigureAwait(false);
        var preservedFamilies = await ReadPreservedFamilyMetadataAsync(connection, tableColumns, cancellationToken, transaction).ConfigureAwait(false);

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
            var tags = preservedFamily is null ? family.Tags : preservedFamily.Tags;
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

    private SqliteConnection CreateConnection() => new($"Data Source={_databasePath}");

    private static async Task EnsureSchemaVersionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var columns = await ReadTableColumnsAsync(connection, SchemaVersionTable, cancellationToken).ConfigureAwait(false);
        if (columns.Count == 0 || IsSchemaIncompatible(SchemaVersionSchema, columns))
        {
            await ExecuteAsync(connection, $"DROP TABLE IF EXISTS {SchemaVersionTable};", cancellationToken).ConfigureAwait(false);
            await ExecuteAsync(connection, SchemaVersionSchema.CreateSql, cancellationToken).ConfigureAwait(false);
        }

        await ExecuteAsync(
            connection,
            "INSERT OR IGNORE INTO schema_version(version, applied_at) VALUES(1, datetime('now'));",
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureCanonicalFontIndexSchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var tableColumns = await ReadFontIndexColumnsAsync(connection, cancellationToken).ConfigureAwait(false);
        var requiresRebuild = FontIndexSchemas.Any(schema =>
            !tableColumns.TryGetValue(schema.Name, out var columns) || IsSchemaIncompatible(schema, columns));
        if (!requiresRebuild)
        {
            requiresRebuild = !await HasUniqueIndexAsync(
                connection,
                FontFacesTable,
                ["family_name", "subfamily_name", "file_path"],
                cancellationToken).ConfigureAwait(false);
        }

        if (!requiresRebuild)
        {
            return;
        }

        await RebuildFontIndexTablesAsync(connection, tableColumns, cancellationToken).ConfigureAwait(false);
    }

    private static async Task RebuildFontIndexTablesAsync(
        SqliteConnection connection,
        IReadOnlyDictionary<string, IReadOnlyList<TableColumn>> tableColumns,
        CancellationToken cancellationToken)
    {
        var preservedFamilies = await ReadPreservedFamilyMetadataAsync(connection, tableColumns, cancellationToken).ConfigureAwait(false);

        await ExecuteAsync(connection, "PRAGMA foreign_keys = OFF;", cancellationToken).ConfigureAwait(false);
        try
        {
            var dbTransaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using var transaction = (SqliteTransaction)dbTransaction;

            await ExecuteAsync(connection, transaction, $"DROP TABLE IF EXISTS {FontFacesTable};", cancellationToken).ConfigureAwait(false);
            await ExecuteAsync(connection, transaction, $"DROP TABLE IF EXISTS {FontFamiliesTable};", cancellationToken).ConfigureAwait(false);
            await ExecuteAsync(connection, transaction, $"DROP TABLE IF EXISTS {FontFilesTable};", cancellationToken).ConfigureAwait(false);

            foreach (var schema in FontIndexSchemas)
            {
                await ExecuteAsync(connection, transaction, schema.CreateSql, cancellationToken).ConfigureAwait(false);
            }

            foreach (var family in preservedFamilies.Values)
            {
                var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = """
                    INSERT INTO font_families(family_name, source_kind, activation_state, license_status, license_text, tags, collections, is_favorite, updated_at)
                    VALUES($familyName, $sourceKind, $activationState, $licenseStatus, $licenseText, $tags, $collections, $isFavorite, datetime('now'));
                    """;
                command.Parameters.AddWithValue("$familyName", family.FamilyName);
                command.Parameters.AddWithValue("$sourceKind", (int)FontSourceKind.Unknown);
                command.Parameters.AddWithValue("$activationState", (int)FontActivationState.Unknown);
                command.Parameters.AddWithValue("$licenseStatus", (int)LicenseStatus.Unknown);
                command.Parameters.AddWithValue("$licenseText", "未知授权");
                command.Parameters.AddWithValue("$tags", JoinList(family.Tags));
                command.Parameters.AddWithValue("$collections", JoinList(family.Collections));
                command.Parameters.AddWithValue("$isFavorite", family.IsFavorite ? 1 : 0);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await ExecuteAsync(connection, "PRAGMA foreign_keys = ON;", cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<Dictionary<string, IReadOnlyList<TableColumn>>> ReadFontIndexColumnsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, IReadOnlyList<TableColumn>>(StringComparer.OrdinalIgnoreCase);
        foreach (var schema in FontIndexSchemas)
        {
            var columns = await ReadTableColumnsAsync(connection, schema.Name, cancellationToken).ConfigureAwait(false);
            if (columns.Count > 0)
            {
                result[schema.Name] = columns;
            }
        }

        return result;
    }

    private static async Task<IReadOnlyList<TableColumn>> ReadTableColumnsAsync(
        SqliteConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        var columns = new List<TableColumn>();
        var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({QuoteIdentifier(tableName)});";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            columns.Add(new TableColumn(
                reader.GetString(1),
                reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                reader.GetInt32(3) == 1,
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetInt32(5)));
        }

        return columns;
    }

    private static async Task<bool> HasUniqueIndexAsync(
        SqliteConnection connection,
        string tableName,
        IReadOnlyList<string> expectedColumns,
        CancellationToken cancellationToken)
    {
        var indexNames = new List<string>();
        var indexListCommand = connection.CreateCommand();
        indexListCommand.CommandText = $"PRAGMA index_list({QuoteIdentifier(tableName)});";
        await using (var reader = await indexListCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (reader.GetInt32(2) == 1)
                {
                    indexNames.Add(reader.GetString(1));
                }
            }
        }

        foreach (var indexName in indexNames)
        {
            var indexColumns = new List<string>();
            var indexInfoCommand = connection.CreateCommand();
            indexInfoCommand.CommandText = $"PRAGMA index_info({QuoteIdentifier(indexName)});";
            await using var reader = await indexInfoCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                indexColumns.Add(reader.GetString(2));
            }

            if (indexColumns.SequenceEqual(expectedColumns, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSchemaIncompatible(CanonicalTableSchema schema, IReadOnlyList<TableColumn> columns)
    {
        var actualByName = columns.ToDictionary(column => column.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var expected in schema.Columns)
        {
            if (!actualByName.TryGetValue(expected.Name, out var actual))
            {
                return true;
            }

            if (!string.Equals(NormalizeSqliteType(actual.Type), expected.Type, StringComparison.OrdinalIgnoreCase)
                || actual.NotNull != expected.NotNull
                || actual.PrimaryKeyOrdinal != expected.PrimaryKeyOrdinal)
            {
                return true;
            }
        }

        return columns.Count != schema.Columns.Count;
    }

    private static string NormalizeSqliteType(string type)
    {
        var normalized = type.Trim().ToUpperInvariant();
        if (normalized.Contains("INT", StringComparison.Ordinal))
        {
            return "INTEGER";
        }

        if (normalized.Contains("CHAR", StringComparison.Ordinal)
            || normalized.Contains("CLOB", StringComparison.Ordinal)
            || normalized.Contains("TEXT", StringComparison.Ordinal))
        {
            return "TEXT";
        }

        return normalized;
    }

    private static async Task<Dictionary<string, PreservedFamilyMetadata>> ReadPreservedFamilyMetadataAsync(
        SqliteConnection connection,
        IReadOnlyDictionary<string, IReadOnlyList<TableColumn>> tableColumns,
        CancellationToken cancellationToken,
        SqliteTransaction? transaction = null)
    {
        var result = new Dictionary<string, PreservedFamilyMetadata>(StringComparer.OrdinalIgnoreCase);
        if (!tableColumns.TryGetValue(FontFamiliesTable, out var columns) || !HasColumn(columns, "family_name"))
        {
            return result;
        }

        var tagsExpression = HasColumn(columns, "tags") ? "tags" : "''";
        var collectionsExpression = HasColumn(columns, "collections") ? "collections" : "''";
        var isFavoriteExpression = HasColumn(columns, "is_favorite") ? "is_favorite" : "0";
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            SELECT family_name, {tagsExpression}, {collectionsExpression}, {isFavoriteExpression}
            FROM font_families
            WHERE family_name IS NOT NULL AND trim(family_name) <> '';
            """;

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var familyName = reader.GetString(0);
                result[familyName] = new PreservedFamilyMetadata(
                    familyName,
                    SplitList(ReadStringOrEmpty(reader, 1)),
                    SplitList(ReadStringOrEmpty(reader, 2)),
                    ReadBoolean(reader, 3));
            }
        }
        catch (SqliteException)
        {
            return [];
        }

        return result;
    }

    private static bool HasColumn(IReadOnlyList<TableColumn> columns, string name) =>
        columns.Any(column => string.Equals(column.Name, name, StringComparison.OrdinalIgnoreCase));

    private static string QuoteIdentifier(string value) => "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task ExecuteAsync(SqliteConnection connection, SqliteTransaction transaction, string sql, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string JoinList(IReadOnlyList<string> value) => string.Join('\u001f', value);

    private static IReadOnlyList<string> SplitList(string value) =>
        string.IsNullOrWhiteSpace(value) ? Array.Empty<string>() : value.Split('\u001f', StringSplitOptions.RemoveEmptyEntries);

    private static string ReadStringOrEmpty(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal)) ?? string.Empty;

    private static bool ReadBoolean(SqliteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return false;
        }

        return reader.GetValue(ordinal) switch
        {
            bool value => value,
            byte value => value != 0,
            short value => value != 0,
            int value => value != 0,
            long value => value != 0,
            string value => value == "1" || bool.TryParse(value, out var parsed) && parsed,
            _ => false
        };
    }

    private static DateTimeOffset ParseLastSeenAt(string value) =>
        DateTimeOffset.TryParse(value, out var parsed) ? parsed : DateTimeOffset.UnixEpoch;

    private static bool Matches(FontFamilyRecord font, FontSearchQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            var search = query.SearchText.Trim();
            var textHit = font.FamilyName.Contains(search, StringComparison.CurrentCultureIgnoreCase)
                || font.Faces.Any(face => face.FullName.Contains(search, StringComparison.CurrentCultureIgnoreCase))
                || font.Tags.Any(tag => tag.Contains(search, StringComparison.CurrentCultureIgnoreCase));
            if (!textHit)
            {
                return false;
            }
        }

        return (query.SourceKind is null || font.SourceKind == query.SourceKind)
            && (query.ActivationState is null || font.ActivationState == query.ActivationState)
            && (string.IsNullOrWhiteSpace(query.Tag) || font.Tags.Contains(query.Tag, StringComparer.CurrentCultureIgnoreCase))
            && (string.IsNullOrWhiteSpace(query.Collection) || font.Collections.Contains(query.Collection, StringComparer.CurrentCultureIgnoreCase))
            && (!query.FavoritesOnly || font.IsFavorite);
    }

    private sealed record MutableFamily(
        string FamilyName,
        FontSourceKind SourceKind,
        FontActivationState ActivationState,
        LicenseStatus LicenseStatus,
        string LicenseText,
        IReadOnlyList<string> Tags,
        IReadOnlyList<string> Collections,
        bool IsFavorite)
    {
        public List<FontFaceRecord> Faces { get; } = [];

        public FontFamilyRecord ToRecord() => new(FamilyName, Faces, SourceKind, ActivationState, LicenseStatus, LicenseText, Tags, Collections, IsFavorite);
    }

    private sealed record CanonicalTableSchema(string Name, IReadOnlyList<CanonicalColumn> Columns, string CreateSql);

    private sealed record CanonicalColumn(string Name, string Type, bool NotNull, int PrimaryKeyOrdinal);

    private sealed record TableColumn(string Name, string Type, bool NotNull, string? DefaultValue, int PrimaryKeyOrdinal);

    private sealed record PreservedFamilyMetadata(
        string FamilyName,
        IReadOnlyList<string> Tags,
        IReadOnlyList<string> Collections,
        bool IsFavorite);
}
