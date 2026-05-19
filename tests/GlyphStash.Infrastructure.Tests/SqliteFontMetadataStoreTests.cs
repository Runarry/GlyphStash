using GlyphStash.Domain.Fonts;
using GlyphStash.Infrastructure.Storage.Sqlite;

namespace GlyphStash.Infrastructure.Tests;

public sealed class SqliteFontMetadataStoreTests
{
    [Fact]
    public async Task SaveAndSearch_RoundTripsFontIndex()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "GlyphStash.Tests", $"{Guid.NewGuid():N}.db");
        var store = new SqliteFontMetadataStore(dbPath);
        var file = new FontFileRecord("C:/Fonts/Inter-Regular.ttf", "TTF", "sha256:test", FontSourceKind.UserInstalled, DateTimeOffset.UtcNow);
        var family = new FontFamilyRecord(
            "Inter",
            [new FontFaceRecord("Inter", "Regular", "Inter Regular", "Inter-Regular", 400, "Normal", "Normal", file)],
            FontSourceKind.UserInstalled,
            FontActivationState.Installed,
            LicenseStatus.Known,
            "SIL Open Font License 1.1",
            ["无衬线", "UI"],
            ["官网改版"],
            true);

        await store.InitializeAsync(CancellationToken.None);
        await store.SaveFontIndexAsync([family], CancellationToken.None);

        var result = await store.SearchAsync(new FontSearchQuery(SearchText: "Inter", SourceKind: FontSourceKind.UserInstalled), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("Inter", result[0].FamilyName);
        Assert.Equal("Inter-Regular", result[0].Faces[0].PostScriptName);
        Assert.Contains("UI", result[0].Tags);
    }

    [Fact]
    public async Task SaveFontIndex_AllowsMultipleInstalledVirtualPaths()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "GlyphStash.Tests", $"{Guid.NewGuid():N}.db");
        var store = new SqliteFontMetadataStore(dbPath);

        var first = CreateInstalledFamily("Arial");
        var second = CreateInstalledFamily("Segoe UI");

        await store.InitializeAsync(CancellationToken.None);
        await store.SaveFontIndexAsync([first, second], CancellationToken.None);

        var result = await store.SearchAsync(new FontSearchQuery(), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.All(result, font => Assert.False(string.IsNullOrWhiteSpace(font.PrimaryFilePath)));
        Assert.Contains(result, font => font.PrimaryFilePath == "installed://Arial");
        Assert.Contains(result, font => font.PrimaryFilePath == "installed://Segoe%20UI");
    }

    [Fact]
    public async Task Initialize_MigratesFontFilesMissingSourceKindColumn()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "GlyphStash.Tests", $"{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}"))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE font_files (
                    path TEXT NOT NULL PRIMARY KEY,
                    format TEXT NOT NULL,
                    sha256 TEXT NULL,
                    last_seen_at TEXT NOT NULL
                );
                """;
            await command.ExecuteNonQueryAsync();
        }

        var store = new SqliteFontMetadataStore(dbPath);
        await store.InitializeAsync(CancellationToken.None);
        await store.SaveFontIndexAsync([CreateInstalledFamily("Arial")], CancellationToken.None);

        var result = await store.SearchAsync(new FontSearchQuery(), CancellationToken.None);
        Assert.Single(result);
        Assert.Equal("installed://Arial", result[0].PrimaryFilePath);
    }

    [Fact]
    public async Task Initialize_MigratesFontFilesMissingLastSeenAtColumn()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "GlyphStash.Tests", $"{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}"))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE font_files (
                    path TEXT NOT NULL PRIMARY KEY,
                    format TEXT NOT NULL,
                    sha256 TEXT NULL,
                    source_kind INTEGER NOT NULL
                );
                """;
            await command.ExecuteNonQueryAsync();
        }

        var store = new SqliteFontMetadataStore(dbPath);
        await store.InitializeAsync(CancellationToken.None);
        await store.SaveFontIndexAsync([CreateInstalledFamily("Arial")], CancellationToken.None);

        var result = await store.SearchAsync(new FontSearchQuery(), CancellationToken.None);
        Assert.Single(result);
        Assert.Equal("installed://Arial", result[0].PrimaryFilePath);
    }

    [Fact]
    public async Task Search_HandlesEmptyLegacyLastSeenAtValue()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "GlyphStash.Tests", $"{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}"))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE font_files (
                    path TEXT NOT NULL PRIMARY KEY,
                    format TEXT NOT NULL,
                    sha256 TEXT NULL,
                    source_kind INTEGER NOT NULL,
                    last_seen_at TEXT NOT NULL
                );

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
                    UNIQUE(family_name, subfamily_name, file_path)
                );

                INSERT INTO font_files(path, format, sha256, source_kind, last_seen_at)
                VALUES('installed://Arial', 'Installed', NULL, 1, '');

                INSERT INTO font_families(family_name, source_kind, activation_state, license_status, license_text, tags, collections, is_favorite, updated_at)
                VALUES('Arial', 1, 1, 0, '未知授权', '', '', 0, '');

                INSERT INTO font_faces(family_name, subfamily_name, full_name, post_script_name, weight, width, slant, file_path)
                VALUES('Arial', 'Regular', 'Arial', 'Arial', 400, 'Normal', 'Normal', 'installed://Arial');
                """;
            await command.ExecuteNonQueryAsync();
        }

        var store = new SqliteFontMetadataStore(dbPath);
        var result = await store.SearchAsync(new FontSearchQuery(), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(DateTimeOffset.UnixEpoch, result[0].Faces[0].File.LastSeenAt);
    }

    [Fact]
    public async Task Initialize_MigratesLegacyFamilyAndFaceColumns()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "GlyphStash.Tests", $"{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}"))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE font_files (
                    path TEXT NOT NULL PRIMARY KEY,
                    format TEXT NOT NULL,
                    sha256 TEXT NULL,
                    last_seen_at TEXT NOT NULL
                );

                CREATE TABLE font_families (
                    family_name TEXT NOT NULL PRIMARY KEY
                );

                CREATE TABLE font_faces (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    family_name TEXT NOT NULL,
                    subfamily_name TEXT NOT NULL,
                    full_name TEXT NOT NULL,
                    file_path TEXT NOT NULL
                );
                """;
            await command.ExecuteNonQueryAsync();
        }

        var store = new SqliteFontMetadataStore(dbPath);
        await store.InitializeAsync(CancellationToken.None);
        await store.SaveFontIndexAsync([CreateInstalledFamily("Segoe UI")], CancellationToken.None);

        var result = await store.SearchAsync(new FontSearchQuery(), CancellationToken.None);
        Assert.Single(result);
        Assert.Equal("Segoe UI", result[0].FamilyName);
        Assert.Equal("installed://Segoe%20UI", result[0].PrimaryFilePath);
    }

    [Fact]
    public async Task Initialize_MigratesFontFacesMissingFilePathColumn()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "GlyphStash.Tests", $"{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}"))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE font_files (
                    path TEXT NOT NULL PRIMARY KEY,
                    format TEXT NOT NULL,
                    sha256 TEXT NULL,
                    source_kind INTEGER NOT NULL,
                    last_seen_at TEXT NOT NULL
                );

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

                CREATE TABLE font_faces (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    family_name TEXT NOT NULL,
                    subfamily_name TEXT NOT NULL,
                    full_name TEXT NOT NULL,
                    post_script_name TEXT NOT NULL,
                    weight INTEGER NOT NULL,
                    width TEXT NOT NULL,
                    slant TEXT NOT NULL
                );
                """;
            await command.ExecuteNonQueryAsync();
        }

        var store = new SqliteFontMetadataStore(dbPath);
        await store.InitializeAsync(CancellationToken.None);
        await store.SaveFontIndexAsync([CreateInstalledFamily("Arial")], CancellationToken.None);

        var result = await store.SearchAsync(new FontSearchQuery(), CancellationToken.None);
        Assert.Single(result);
        Assert.Equal("installed://Arial", result[0].PrimaryFilePath);
    }

    [Fact]
    public async Task Initialize_RebuildsFontFilesWithLegacyNotNullFamilyIdColumn()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "GlyphStash.Tests", $"{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}"))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE font_files (
                    path TEXT NOT NULL PRIMARY KEY,
                    family_id TEXT NOT NULL,
                    format TEXT NOT NULL,
                    sha256 TEXT NULL,
                    source_kind INTEGER NOT NULL,
                    last_seen_at TEXT NOT NULL
                );
                """;
            await command.ExecuteNonQueryAsync();
        }

        var store = new SqliteFontMetadataStore(dbPath);
        await store.InitializeAsync(CancellationToken.None);
        await store.SaveFontIndexAsync([CreateInstalledFamily("Arial")], CancellationToken.None);

        var result = await store.SearchAsync(new FontSearchQuery(), CancellationToken.None);
        Assert.Single(result);
        Assert.Equal("installed://Arial", result[0].PrimaryFilePath);
    }

    [Fact]
    public async Task Initialize_RebuildsLegacyTablesWithExtraNotNullColumnsAndPreservesFamilyMetadata()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "GlyphStash.Tests", $"{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}"))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE font_files (
                    path TEXT NOT NULL PRIMARY KEY,
                    family_id TEXT NOT NULL,
                    format TEXT NOT NULL,
                    sha256 TEXT NULL,
                    source_kind INTEGER NOT NULL,
                    last_seen_at TEXT NOT NULL
                );

                CREATE TABLE font_families (
                    family_name TEXT NOT NULL PRIMARY KEY,
                    source_kind INTEGER NOT NULL,
                    activation_state INTEGER NOT NULL,
                    license_status INTEGER NOT NULL,
                    license_text TEXT NOT NULL,
                    tags TEXT NOT NULL,
                    collections TEXT NOT NULL,
                    is_favorite INTEGER NOT NULL DEFAULT 0,
                    updated_at TEXT NOT NULL,
                    legacy_required TEXT NOT NULL
                );

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
                    legacy_required TEXT NOT NULL
                );

                INSERT INTO font_families(family_name, source_kind, activation_state, license_status, license_text, tags, collections, is_favorite, updated_at, legacy_required)
                VALUES('Arial', 1, 1, 0, '未知授权', '系统字' || char(31) || '正文', '品牌库', 1, '', 'legacy');
                """;
            await command.ExecuteNonQueryAsync();
        }

        var store = new SqliteFontMetadataStore(dbPath);
        await store.InitializeAsync(CancellationToken.None);
        await store.SaveFontIndexAsync([CreateInstalledFamily("Arial")], CancellationToken.None);

        var result = await store.SearchAsync(new FontSearchQuery(), CancellationToken.None);
        Assert.Single(result);
        Assert.Contains("系统字", result[0].Tags);
        Assert.Contains("正文", result[0].Tags);
        Assert.Contains("品牌库", result[0].Collections);
        Assert.True(result[0].IsFavorite);
    }

    [Fact]
    public async Task SaveFontIndex_UpsertsDuplicatePathsAndFaces()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "GlyphStash.Tests", $"{Guid.NewGuid():N}.db");
        var store = new SqliteFontMetadataStore(dbPath);
        var sharedFile = new FontFileRecord("C:/Fonts/Shared.ttc", "TTC", null, FontSourceKind.System, DateTimeOffset.UtcNow);
        var family = new FontFamilyRecord(
            "Shared",
            [
                new FontFaceRecord("Shared", "Regular", "Shared Regular", "Shared-Regular", 400, "Normal", "Normal", sharedFile),
                new FontFaceRecord("Shared", "Regular", "Shared Regular", "Shared-Regular", 400, "Normal", "Normal", sharedFile)
            ],
            FontSourceKind.System,
            FontActivationState.Installed,
            LicenseStatus.Unknown,
            "未知授权",
            [],
            [],
            false);

        await store.InitializeAsync(CancellationToken.None);
        await store.SaveFontIndexAsync([family, family], CancellationToken.None);

        var result = await store.SearchAsync(new FontSearchQuery(), CancellationToken.None);
        Assert.Single(result);
        Assert.Single(result[0].Faces);
        Assert.Equal("C:/Fonts/Shared.ttc", result[0].PrimaryFilePath);
    }

    [Fact]
    public async Task Initialize_RebuildsFontFacesMissingUniqueConstraint()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "GlyphStash.Tests", $"{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}"))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE font_files (
                    path TEXT NOT NULL PRIMARY KEY,
                    format TEXT NOT NULL,
                    sha256 TEXT NULL,
                    source_kind INTEGER NOT NULL,
                    last_seen_at TEXT NOT NULL
                );

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

                CREATE TABLE font_faces (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    family_name TEXT NOT NULL,
                    subfamily_name TEXT NOT NULL,
                    full_name TEXT NOT NULL,
                    post_script_name TEXT NOT NULL,
                    weight INTEGER NOT NULL,
                    width TEXT NOT NULL,
                    slant TEXT NOT NULL,
                    file_path TEXT NOT NULL
                );
                """;
            await command.ExecuteNonQueryAsync();
        }

        var store = new SqliteFontMetadataStore(dbPath);
        await store.InitializeAsync(CancellationToken.None);
        await store.SaveFontIndexAsync([CreateInstalledFamily("Arial")], CancellationToken.None);

        var result = await store.SearchAsync(new FontSearchQuery(), CancellationToken.None);
        Assert.Single(result);
        Assert.Equal("installed://Arial", result[0].PrimaryFilePath);
    }

    [Fact]
    public async Task Initialize_RebuildsLegacySchemaVersionTable()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "GlyphStash.Tests", $"{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}"))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE schema_version (
                    version INTEGER NOT NULL PRIMARY KEY,
                    applied_at TEXT NOT NULL,
                    legacy_required TEXT NOT NULL
                );
                """;
            await command.ExecuteNonQueryAsync();
        }

        var store = new SqliteFontMetadataStore(dbPath);
        await store.InitializeAsync(CancellationToken.None);
        await store.SaveFontIndexAsync([CreateInstalledFamily("Arial")], CancellationToken.None);

        var result = await store.SearchAsync(new FontSearchQuery(), CancellationToken.None);
        Assert.Single(result);
        Assert.Equal("installed://Arial", result[0].PrimaryFilePath);
    }

    private static FontFamilyRecord CreateInstalledFamily(string familyName)
    {
        var file = new FontFileRecord($"installed://{Uri.EscapeDataString(familyName)}", "Installed", null, FontSourceKind.System, DateTimeOffset.UtcNow);
        return new FontFamilyRecord(
            familyName,
            [new FontFaceRecord(familyName, "Regular", familyName, familyName.Replace(' ', '-'), 400, "Normal", "Normal", file)],
            FontSourceKind.System,
            FontActivationState.Installed,
            LicenseStatus.Unknown,
            "未知授权",
            [],
            [],
            false);
    }
}
