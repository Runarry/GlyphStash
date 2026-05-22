using GlyphStash.Application.Abstractions.Storage;
using GlyphStash.Domain.Fonts;
using Microsoft.Data.Sqlite;

namespace GlyphStash.Infrastructure.Storage.Sqlite;

public sealed partial class SqliteFontMetadataStore :
    IFontMetadataStore,
    IAppSettingsStore,
    IFontLibraryMutationStore,
    ITagStore,
    ICollectionStore,
    IActivationStore,
    IOperationLogStore,
    IDownloadRecordStore
{
    private static readonly string[] DefaultTags = ["中文", "英文", "衬线", "无衬线", "等宽", "可商用", "开源", "品牌项目"];

    private const string SchemaVersionTable = "schema_version";
    private const string FontFilesTable = "font_files";
    private const string FontFamiliesTable = "font_families";
    private const string FontFacesTable = "font_faces";
    private const string AppSettingsTable = "app_settings";
    private const string ManagedFontsTable = "managed_fonts";
    private const string TagsTable = "tags";
    private const string FontTagsTable = "font_tags";
    private const string CollectionsTable = "collections";
    private const string CollectionFontsTable = "collection_fonts";
    private const string LegacyFontCollectionItemsTable = "font_collection_items";
    private const string ActivationRecordsTable = "activation_records";
    private const string OperationLogsTable = "operation_logs";
    private const string DownloadRecordsTable = "download_records";

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
        await EnsureM2SchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        _initialized = true;
    }

    private static IReadOnlySet<string> GetCurrentManagedFilePaths(IReadOnlyList<FontFamilyRecord> fonts) =>
        fonts
            .SelectMany(font => font.Faces)
            .Select(face => face.File)
            .Where(file => file.SourceKind == FontSourceKind.GlyphStashManaged && !string.IsNullOrWhiteSpace(file.Path))
            .Select(file => file.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static async Task PruneManagedFontRecordsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IReadOnlySet<string> currentManagedFilePaths,
        CancellationToken cancellationToken)
    {
        var existsCommand = connection.CreateCommand();
        existsCommand.Transaction = transaction;
        existsCommand.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = $tableName;";
        existsCommand.Parameters.AddWithValue("$tableName", ManagedFontsTable);
        var exists = await existsCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
        if (string.IsNullOrWhiteSpace(exists))
        {
            return;
        }

        var existingPaths = new List<string>();
        var selectCommand = connection.CreateCommand();
        selectCommand.Transaction = transaction;
        selectCommand.CommandText = "SELECT managed_file_path FROM managed_fonts;";
        await using (var reader = await selectCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                existingPaths.Add(reader.GetString(0));
            }
        }

        foreach (var stalePath in existingPaths.Where(path => !currentManagedFilePaths.Contains(path)))
        {
            await ExecuteAsync(
                connection,
                transaction,
                "DELETE FROM managed_fonts WHERE managed_file_path = $path;",
                [new("$path", stalePath)],
                cancellationToken).ConfigureAwait(false);
        }
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

    private static async Task EnsureM2SchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await RebuildIncompatibleM2TablesAsync(connection, cancellationToken).ConfigureAwait(false);

        var statements = new[]
        {
            """
            CREATE TABLE IF NOT EXISTS app_settings (
                key TEXT NOT NULL PRIMARY KEY,
                value TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS managed_fonts (
                managed_file_path TEXT NOT NULL PRIMARY KEY,
                family_name TEXT NOT NULL,
                format TEXT NOT NULL,
                sha256 TEXT NULL,
                installed_file_path TEXT NULL,
                activation_state INTEGER NOT NULL,
                imported_at TEXT NOT NULL,
                installed_at TEXT NULL
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS tags (
                name TEXT NOT NULL PRIMARY KEY,
                created_at TEXT NOT NULL
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS font_tags (
                family_name TEXT NOT NULL,
                tag_name TEXT NOT NULL,
                PRIMARY KEY(family_name, tag_name),
                FOREIGN KEY(tag_name) REFERENCES tags(name) ON DELETE CASCADE
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS collections (
                name TEXT NOT NULL PRIMARY KEY,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                last_exported_at TEXT NULL
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS collection_fonts (
                collection_name TEXT NOT NULL,
                family_name TEXT NOT NULL,
                PRIMARY KEY(collection_name, family_name),
                FOREIGN KEY(collection_name) REFERENCES collections(name) ON DELETE CASCADE
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS activation_records (
                font_path TEXT NOT NULL,
                owner_key TEXT NOT NULL,
                reference_count INTEGER NOT NULL,
                platform_flags INTEGER NOT NULL,
                activated_at TEXT NOT NULL,
                last_known_state TEXT NOT NULL,
                cleanup_status TEXT NOT NULL,
                sha256 TEXT NULL,
                scope TEXT NOT NULL,
                PRIMARY KEY(font_path, owner_key)
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS operation_logs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp TEXT NOT NULL,
                category TEXT NOT NULL,
                action TEXT NOT NULL,
                message TEXT NOT NULL,
                target TEXT NULL,
                succeeded INTEGER NOT NULL
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS download_records (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                provider_id TEXT NOT NULL,
                remote_id TEXT NOT NULL,
                family_name TEXT NOT NULL,
                variant TEXT NOT NULL,
                download_url TEXT NOT NULL,
                source_url TEXT NOT NULL,
                license_text TEXT NOT NULL,
                local_file_path TEXT NOT NULL,
                downloaded_at TEXT NOT NULL
            );
            """,
            "CREATE INDEX IF NOT EXISTS ix_operation_logs_timestamp ON operation_logs(timestamp DESC);",
            "CREATE INDEX IF NOT EXISTS ix_download_records_downloaded_at ON download_records(downloaded_at DESC);"
        };

        foreach (var statement in statements)
        {
            await ExecuteAsync(connection, statement, cancellationToken).ConfigureAwait(false);
        }

        await MigrateFamilyMetadataToM2TablesAsync(connection, cancellationToken).ConfigureAwait(false);
        await RemoveLegacyCollectionRelationshipTablesAsync(connection, cancellationToken).ConfigureAwait(false);
        await EnsureDefaultTagsAsync(connection, cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, "INSERT OR IGNORE INTO schema_version(version, applied_at) VALUES(3, datetime('now'));", cancellationToken).ConfigureAwait(false);
    }

    private static async Task RebuildIncompatibleM2TablesAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var schemas = new[]
        {
            new CanonicalTableSchema(AppSettingsTable, [new("key", "TEXT", true, 1), new("value", "TEXT", true, 0), new("updated_at", "TEXT", true, 0)], ""),
            new CanonicalTableSchema(ManagedFontsTable, [new("managed_file_path", "TEXT", true, 1), new("family_name", "TEXT", true, 0), new("format", "TEXT", true, 0), new("sha256", "TEXT", false, 0), new("installed_file_path", "TEXT", false, 0), new("activation_state", "INTEGER", true, 0), new("imported_at", "TEXT", true, 0), new("installed_at", "TEXT", false, 0)], ""),
            new CanonicalTableSchema(TagsTable, [new("name", "TEXT", true, 1), new("created_at", "TEXT", true, 0)], ""),
            new CanonicalTableSchema(FontTagsTable, [new("family_name", "TEXT", true, 1), new("tag_name", "TEXT", true, 2)], ""),
            new CanonicalTableSchema(CollectionsTable, [new("name", "TEXT", true, 1), new("created_at", "TEXT", true, 0), new("updated_at", "TEXT", true, 0), new("last_exported_at", "TEXT", false, 0)], ""),
            new CanonicalTableSchema(CollectionFontsTable, [new("collection_name", "TEXT", true, 1), new("family_name", "TEXT", true, 2)], ""),
            new CanonicalTableSchema(ActivationRecordsTable, [new("font_path", "TEXT", true, 1), new("owner_key", "TEXT", true, 2), new("reference_count", "INTEGER", true, 0), new("platform_flags", "INTEGER", true, 0), new("activated_at", "TEXT", true, 0), new("last_known_state", "TEXT", true, 0), new("cleanup_status", "TEXT", true, 0), new("sha256", "TEXT", false, 0), new("scope", "TEXT", true, 0)], ""),
            new CanonicalTableSchema(OperationLogsTable, [new("id", "INTEGER", false, 1), new("timestamp", "TEXT", true, 0), new("category", "TEXT", true, 0), new("action", "TEXT", true, 0), new("message", "TEXT", true, 0), new("target", "TEXT", false, 0), new("succeeded", "INTEGER", true, 0)], ""),
            new CanonicalTableSchema(DownloadRecordsTable, [new("id", "INTEGER", false, 1), new("provider_id", "TEXT", true, 0), new("remote_id", "TEXT", true, 0), new("family_name", "TEXT", true, 0), new("variant", "TEXT", true, 0), new("download_url", "TEXT", true, 0), new("source_url", "TEXT", true, 0), new("license_text", "TEXT", true, 0), new("local_file_path", "TEXT", true, 0), new("downloaded_at", "TEXT", true, 0)], "")
        };

        var dropNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var schema in schemas)
        {
            var columns = await ReadTableColumnsAsync(connection, schema.Name, cancellationToken).ConfigureAwait(false);
            if (columns.Count > 0 && IsSchemaIncompatible(schema, columns))
            {
                dropNames.Add(schema.Name);
            }
        }

        if (dropNames.Contains(TagsTable))
        {
            dropNames.Add(FontTagsTable);
        }

        if (dropNames.Contains(CollectionsTable))
        {
            dropNames.Add(CollectionFontsTable);
        }

        if (dropNames.Count == 0)
        {
            return;
        }

        await ExecuteAsync(connection, "PRAGMA foreign_keys = OFF;", cancellationToken).ConfigureAwait(false);
        try
        {
            var dbTransaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using var transaction = (SqliteTransaction)dbTransaction;
            foreach (var tableName in new[]
            {
                FontTagsTable,
                CollectionFontsTable,
                ActivationRecordsTable,
                OperationLogsTable,
                DownloadRecordsTable,
                ManagedFontsTable,
                TagsTable,
                CollectionsTable,
                AppSettingsTable
            })
            {
                if (dropNames.Contains(tableName))
                {
                    await ExecuteAsync(connection, transaction, $"DROP TABLE IF EXISTS {QuoteIdentifier(tableName)};", cancellationToken).ConfigureAwait(false);
                }
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await ExecuteAsync(connection, "PRAGMA foreign_keys = ON;", cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task MigrateFamilyMetadataToM2TablesAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = "SELECT family_name, tags, collections FROM font_families;";
        var families = new List<(string FamilyName, IReadOnlyList<string> Tags, IReadOnlyList<string> Collections)>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                families.Add((reader.GetString(0), SplitList(reader.GetString(1)), SplitList(reader.GetString(2))));
            }
        }

        var dbTransaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)dbTransaction;
        foreach (var family in families)
        {
            foreach (var tag in NormalizeNames(family.Tags))
            {
                await UpsertTagAsync(connection, transaction, tag, cancellationToken).ConfigureAwait(false);
                await ExecuteAsync(connection, transaction, "INSERT OR IGNORE INTO font_tags(family_name, tag_name) VALUES($familyName, $tagName);", [new("$familyName", family.FamilyName), new("$tagName", tag)], cancellationToken).ConfigureAwait(false);
            }

            foreach (var collection in NormalizeNames(family.Collections))
            {
                await UpsertCollectionAsync(connection, transaction, collection, cancellationToken).ConfigureAwait(false);
                await ExecuteAsync(connection, transaction, "INSERT OR IGNORE INTO collection_fonts(collection_name, family_name) VALUES($collectionName, $familyName);", [new("$collectionName", collection), new("$familyName", family.FamilyName)], cancellationToken).ConfigureAwait(false);
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task RemoveLegacyCollectionRelationshipTablesAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, LegacyFontCollectionItemsTable, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        await ExecuteAsync(connection, "PRAGMA foreign_keys = OFF;", cancellationToken).ConfigureAwait(false);
        try
        {
            var dbTransaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using var transaction = (SqliteTransaction)dbTransaction;
            await MigrateLegacyFontCollectionItemsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            await ExecuteAsync(connection, transaction, $"DROP TABLE IF EXISTS {QuoteIdentifier(LegacyFontCollectionItemsTable)};", cancellationToken).ConfigureAwait(false);
            await RebuildFamilyCollectionSummariesAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await ExecuteAsync(connection, "PRAGMA foreign_keys = ON;", cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task MigrateLegacyFontCollectionItemsAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        var columns = await ReadTableColumnsAsync(connection, LegacyFontCollectionItemsTable, cancellationToken).ConfigureAwait(false);
        if (!HasColumn(columns, "collection_name") || !HasColumn(columns, "family_name"))
        {
            return;
        }

        await ExecuteAsync(
            connection,
            transaction,
            $"""
            INSERT OR IGNORE INTO collection_fonts(collection_name, family_name)
            SELECT collection_name, family_name
            FROM {QuoteIdentifier(LegacyFontCollectionItemsTable)}
            WHERE collection_name IS NOT NULL
              AND trim(collection_name) <> ''
              AND family_name IS NOT NULL
              AND trim(family_name) <> '';
            """,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = $tableName;";
        command.Parameters.AddWithValue("$tableName", tableName);
        var existing = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
        return !string.IsNullOrWhiteSpace(existing);
    }

    private static async Task EnsureDefaultTagsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var seededCommand = connection.CreateCommand();
        seededCommand.CommandText = "SELECT value FROM app_settings WHERE key = 'default_tags_seeded';";
        var seeded = await seededCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
        if (seeded == "1")
        {
            return;
        }

        var dbTransaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)dbTransaction;
        foreach (var tag in DefaultTags)
        {
            await UpsertTagAsync(connection, transaction, tag, cancellationToken).ConfigureAwait(false);
        }

        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO app_settings(key, value, updated_at)
            VALUES('default_tags_seeded', '1', datetime('now'))
            ON CONFLICT(key) DO UPDATE SET value = excluded.value, updated_at = excluded.updated_at;
            """,
            cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
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

    private static async Task<IReadOnlyList<FontFamilyRecord>> ReadManagedFamilyRecordsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        var existsCommand = connection.CreateCommand();
        existsCommand.Transaction = transaction;
        existsCommand.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = $tableName;";
        existsCommand.Parameters.AddWithValue("$tableName", ManagedFontsTable);
        var exists = await existsCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
        if (string.IsNullOrWhiteSpace(exists))
        {
            return [];
        }

        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT mf.family_name, mf.managed_file_path, mf.format, mf.sha256, mf.activation_state, mf.installed_file_path,
                   ff.license_status, ff.license_text, ff.tags, ff.collections, ff.is_favorite,
                   fc.subfamily_name, fc.full_name, fc.post_script_name, fc.weight, fc.width, fc.slant
            FROM managed_fonts mf
            LEFT JOIN font_families ff ON ff.family_name = mf.family_name
            LEFT JOIN font_faces fc ON fc.family_name = mf.family_name AND (fc.file_path = mf.managed_file_path OR fc.file_path = mf.installed_file_path)
            ORDER BY mf.family_name COLLATE NOCASE;
            """;

        var result = new List<FontFamilyRecord>();
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var familyName = reader.GetString(0);
                var visiblePath = reader.IsDBNull(5) ? reader.GetString(1) : reader.GetString(5);
                var activationState = (FontActivationState)reader.GetInt32(4);
                if (activationState == FontActivationState.TemporarilyEnabled)
                {
                    activationState = FontActivationState.NotEnabled;
                }

                var file = new FontFileRecord(
                    visiblePath,
                    reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.IsDBNull(5) ? FontSourceKind.GlyphStashManaged : FontSourceKind.UserInstalled,
                    DateTimeOffset.UtcNow);
                var subfamily = reader.IsDBNull(11) ? "Regular" : reader.GetString(11);
                var face = new FontFaceRecord(
                    familyName,
                    subfamily,
                    reader.IsDBNull(12) ? $"{familyName} {subfamily}".Trim() : reader.GetString(12),
                    reader.IsDBNull(13) ? $"{familyName.Replace(' ', '-')}-{subfamily.Replace(' ', '-')}" : reader.GetString(13),
                    reader.IsDBNull(14) ? 400 : reader.GetInt32(14),
                    reader.IsDBNull(15) ? "Normal" : reader.GetString(15),
                    reader.IsDBNull(16) ? "Normal" : reader.GetString(16),
                    file);
                result.Add(new FontFamilyRecord(
                    familyName,
                    [face],
                    reader.IsDBNull(5) ? FontSourceKind.GlyphStashManaged : FontSourceKind.UserInstalled,
                    activationState,
                    reader.IsDBNull(6) ? LicenseStatus.Unknown : (LicenseStatus)reader.GetInt32(6),
                    reader.IsDBNull(7) ? "未知授权" : reader.GetString(7),
                    reader.IsDBNull(8) ? [] : SplitList(reader.GetString(8)),
                    reader.IsDBNull(9) ? [] : SplitList(reader.GetString(9)),
                    !reader.IsDBNull(10) && reader.GetInt32(10) == 1));
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

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string sql,
        IReadOnlyList<SqliteParameterValue> parameters,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        }

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpsertTagAsync(SqliteConnection connection, SqliteTransaction? transaction, string tag, CancellationToken cancellationToken)
    {
        var normalized = tag.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        await ExecuteAsync(
            connection,
            transaction,
            "INSERT OR IGNORE INTO tags(name, created_at) VALUES($name, datetime('now'));",
            [new("$name", normalized)],
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpsertCollectionAsync(SqliteConnection connection, SqliteTransaction? transaction, string collection, CancellationToken cancellationToken)
    {
        var normalized = collection.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO collections(name, created_at, updated_at)
            VALUES($name, datetime('now'), datetime('now'))
            ON CONFLICT(name) DO UPDATE SET updated_at = excluded.updated_at;
            """,
            [new("$name", normalized)],
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task ReplaceFamilyTagsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string familyName,
        IReadOnlyList<string> tags,
        CancellationToken cancellationToken)
    {
        var normalizedTags = NormalizeNames(tags);
        await ExecuteAsync(connection, transaction, "DELETE FROM font_tags WHERE family_name = $familyName;", [new("$familyName", familyName)], cancellationToken).ConfigureAwait(false);
        foreach (var tag in normalizedTags)
        {
            await UpsertTagAsync(connection, transaction, tag, cancellationToken).ConfigureAwait(false);
            await ExecuteAsync(
                connection,
                transaction,
                "INSERT OR IGNORE INTO font_tags(family_name, tag_name) VALUES($familyName, $tagName);",
                [new("$familyName", familyName), new("$tagName", tag)],
                cancellationToken).ConfigureAwait(false);
        }

        await ExecuteAsync(
            connection,
            transaction,
            "UPDATE font_families SET tags = $tags, updated_at = datetime('now') WHERE family_name = $familyName;",
            [new("$tags", JoinList(normalizedTags)), new("$familyName", familyName)],
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task ReplaceFamilyCollectionsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string familyName,
        IReadOnlyList<string> collections,
        CancellationToken cancellationToken)
    {
        var normalizedCollections = NormalizeNames(collections);
        await ExecuteAsync(connection, transaction, "DELETE FROM collection_fonts WHERE family_name = $familyName;", [new("$familyName", familyName)], cancellationToken).ConfigureAwait(false);
        foreach (var collection in normalizedCollections)
        {
            await UpsertCollectionAsync(connection, transaction, collection, cancellationToken).ConfigureAwait(false);
            await ExecuteAsync(
                connection,
                transaction,
                "INSERT OR IGNORE INTO collection_fonts(collection_name, family_name) VALUES($collectionName, $familyName);",
                [new("$collectionName", collection), new("$familyName", familyName)],
                cancellationToken).ConfigureAwait(false);
        }

        await ExecuteAsync(
            connection,
            transaction,
            "UPDATE font_families SET collections = $collections, updated_at = datetime('now') WHERE family_name = $familyName;",
            [new("$collections", JoinList(normalizedCollections)), new("$familyName", familyName)],
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task RebuildFamilyTagSummariesAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT family_name, group_concat(tag_name, char(31))
            FROM font_tags
            GROUP BY family_name;
            """;
        var summaries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                summaries[reader.GetString(0)] = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            }
        }

        await ExecuteAsync(connection, transaction, "UPDATE font_families SET tags = '';", cancellationToken).ConfigureAwait(false);
        foreach (var summary in summaries)
        {
            await ExecuteAsync(
                connection,
                transaction,
                "UPDATE font_families SET tags = $tags, updated_at = datetime('now') WHERE family_name = $familyName;",
                [new("$tags", summary.Value), new("$familyName", summary.Key)],
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task RebuildFamilyCollectionSummariesAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT family_name, group_concat(collection_name, char(31))
            FROM collection_fonts
            GROUP BY family_name;
            """;
        var summaries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                summaries[reader.GetString(0)] = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            }
        }

        await ExecuteAsync(connection, transaction, "UPDATE font_families SET collections = '';", cancellationToken).ConfigureAwait(false);
        foreach (var summary in summaries)
        {
            await ExecuteAsync(
                connection,
                transaction,
                "UPDATE font_families SET collections = $collections, updated_at = datetime('now') WHERE family_name = $familyName;",
                [new("$collections", summary.Value), new("$familyName", summary.Key)],
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static string JoinList(IReadOnlyList<string> value) => string.Join('\u001f', value);

    private static IReadOnlyList<string> SplitList(string value) =>
        string.IsNullOrWhiteSpace(value) ? Array.Empty<string>() : value.Split('\u001f', StringSplitOptions.RemoveEmptyEntries);

    private static IReadOnlyList<string> NormalizeNames(IEnumerable<string> value) =>
        value.Select(item => item.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    private static IReadOnlyList<string> MergeNames(IEnumerable<string> first, IEnumerable<string> second) =>
        first.Concat(second)
            .Select(item => item.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    private static IReadOnlyList<string> InferAutomaticTags(FontFamilyRecord family)
    {
        var searchableText = string.Join(
            ' ',
            family.FamilyName,
            string.Join(' ', family.Faces.Select(face => $"{face.SubfamilyName} {face.FullName} {face.PostScriptName}")));

        return LooksLikeCjkFont(searchableText) ? ["中文"] : [];
    }

    private static bool LooksLikeCjkFont(string value) =>
        value.Contains("CJK", StringComparison.OrdinalIgnoreCase)
        || ContainsSeparatedToken(value, "SC")
        || ContainsSeparatedToken(value, "TC")
        || ContainsSeparatedToken(value, "CN")
        || ContainsSeparatedToken(value, "GB")
        || ContainsSeparatedToken(value, "GBK")
        || value.Contains("Hans", StringComparison.OrdinalIgnoreCase)
        || value.Contains("Hant", StringComparison.OrdinalIgnoreCase)
        || value.Contains("Chinese", StringComparison.OrdinalIgnoreCase)
        || value.Contains("中文", StringComparison.Ordinal)
        || value.Contains("宋体", StringComparison.Ordinal)
        || value.Contains("黑体", StringComparison.Ordinal)
        || value.Contains("楷体", StringComparison.Ordinal)
        || value.Contains("仿宋", StringComparison.Ordinal)
        || value.Contains("雅黑", StringComparison.Ordinal)
        || value.Contains("思源", StringComparison.Ordinal)
        || value.Contains("方正", StringComparison.Ordinal)
        || value.Contains("华文", StringComparison.Ordinal);

    private static bool ContainsSeparatedToken(string value, string token) =>
        value.Split([' ', '-', '_', '.', '(', ')', '[', ']'], StringSplitOptions.RemoveEmptyEntries)
            .Any(part => string.Equals(part, token, StringComparison.OrdinalIgnoreCase));

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

    private sealed record SqliteParameterValue(string Name, object? Value);
}
