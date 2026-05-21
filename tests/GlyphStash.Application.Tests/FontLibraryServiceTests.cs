using GlyphStash.Application.Abstractions.Fonts;
using GlyphStash.Application.Abstractions.Storage;
using GlyphStash.Application.Fonts;
using GlyphStash.Domain.Fonts;

namespace GlyphStash.Application.Tests;

public sealed class FontLibraryServiceTests
{
    [Fact]
    public async Task RescanAsync_IncludesManagedDirectoryFontsWithMetadataNames()
    {
        var store = new FakeMetadataStore();
        var service = CreateService(
            store: store,
            managedPaths: ["C:/GlyphStash/fonts/abc123-SourceName.ttf"],
            metadata:
            [
                new FontMetadata(
                    "C:/GlyphStash/fonts/abc123-SourceName.ttf",
                    "TTF",
                    "Brand Sans",
                    "Regular",
                    "Brand Sans Regular",
                    "BrandSans-Regular",
                    null,
                    null,
                    null,
                    "hash-brand")
            ]);

        var fonts = await service.RescanAsync(CancellationToken.None);

        var font = Assert.Single(fonts);
        Assert.Equal("Brand Sans", font.FamilyName);
        Assert.Equal("Brand Sans Regular", font.Faces[0].FullName);
        Assert.Equal("C:/GlyphStash/fonts/abc123-SourceName.ttf", font.PrimaryFilePath);
        Assert.Equal(FontSourceKind.GlyphStashManaged, font.SourceKind);
        Assert.Equal(FontActivationState.NotEnabled, font.ActivationState);
        Assert.Equal(store.SavedFonts, fonts);
    }

    [Fact]
    public async Task RescanAsync_UsesManagedFontMetadataStyleMetrics()
    {
        var service = CreateService(
            managedPaths: ["C:/GlyphStash/fonts/NotoSans-BoldItalic.ttf"],
            metadata:
            [
                new FontMetadata(
                    "C:/GlyphStash/fonts/NotoSans-BoldItalic.ttf",
                    "TTF",
                    "Noto Sans",
                    "Bold Italic",
                    "Noto Sans Bold Italic",
                    "NotoSans-BoldItalic",
                    null,
                    null,
                    null,
                    "hash-bold",
                    700,
                    "Normal",
                    "Italic")
            ]);

        var fonts = await service.RescanAsync(CancellationToken.None);

        var face = Assert.Single(Assert.Single(fonts).Faces);
        Assert.Equal(700, face.Weight);
        Assert.Equal("Italic", face.Slant);
    }

    [Fact]
    public async Task RescanAsync_ReturnsPersistedMetadataAfterSavingIndex()
    {
        var scanned = CreateInstalledFont("Inter", "C:/Windows/Fonts/Inter.ttf", "hash-inter");
        var persisted = scanned with
        {
            Tags = ["UI"],
            Collections = ["官网改版"],
            IsFavorite = true
        };
        var store = new FakeMetadataStore { SearchResultOverride = [persisted] };
        var service = CreateService(installedFonts: [scanned], store: store);

        var fonts = await service.RescanAsync(CancellationToken.None);

        var font = Assert.Single(fonts);
        Assert.Empty(Assert.Single(store.SavedFonts!).Tags);
        Assert.Contains("UI", font.Tags);
        Assert.Contains("官网改版", font.Collections);
        Assert.True(font.IsFavorite);
    }

    [Fact]
    public async Task LoadCachedFontsAsync_ProjectsActiveTemporaryActivationByPath()
    {
        var cached = CreateInstalledFont("Brand Sans", "C:/GlyphStash/fonts/BrandSans.ttf", "hash-brand") with
        {
            ActivationState = FontActivationState.NotEnabled,
            SourceKind = FontSourceKind.GlyphStashManaged
        };
        var store = new FakeMetadataStore { SearchResultOverride = [cached] };
        var activationStore = new FakeActivationStore(
        [
            new ActivationRecord(
                "C:/GlyphStash/fonts/BrandSans.ttf",
                "font:Brand Sans",
                1,
                0,
                DateTimeOffset.UtcNow,
                "Active",
                "",
                "hash-brand")
        ]);
        var service = CreateService(store: store, activationStore: activationStore);

        var fonts = await service.LoadCachedFontsAsync(new FontSearchQuery(), CancellationToken.None);

        Assert.Equal(FontActivationState.TemporarilyEnabled, Assert.Single(fonts).ActivationState);
    }

    [Fact]
    public async Task RescanAsync_PreservesActiveTemporaryActivationByPath()
    {
        var activationStore = new FakeActivationStore(
        [
            new ActivationRecord(
                "C:/GlyphStash/fonts/BrandSans.ttf",
                "font:Brand Sans",
                1,
                0,
                DateTimeOffset.UtcNow,
                "Active",
                "",
                "hash-brand")
        ]);
        var service = CreateService(
            managedPaths: ["C:/GlyphStash/fonts/BrandSans.ttf"],
            metadata: [CreateMetadata("C:/GlyphStash/fonts/BrandSans.ttf", "Brand Sans", "hash-brand")],
            activationStore: activationStore);

        var fonts = await service.RescanAsync(CancellationToken.None);

        Assert.Equal(FontActivationState.TemporarilyEnabled, Assert.Single(fonts).ActivationState);
        Assert.False(activationStore.RemovedAny);
        Assert.False(activationStore.MarkedStale);
    }

    [Fact]
    public async Task RescanAsync_PreservesActiveTemporaryActivationByHash()
    {
        var service = CreateService(
            managedPaths: ["C:/GlyphStash/fonts/copied-name.ttf"],
            metadata: [CreateMetadata("C:/GlyphStash/fonts/copied-name.ttf", "Brand Sans", "hash-brand")],
            activationStore: new FakeActivationStore(
            [
                new ActivationRecord(
                    "D:/Original/BrandSans.ttf",
                    "font:Brand Sans",
                    1,
                    0,
                    DateTimeOffset.UtcNow,
                    "Active",
                    "",
                    "hash-brand")
            ]));

        var fonts = await service.RescanAsync(CancellationToken.None);

        Assert.Equal(FontActivationState.TemporarilyEnabled, Assert.Single(fonts).ActivationState);
    }

    [Fact]
    public async Task RescanAsync_DoesNotApplyStaleTemporaryActivation()
    {
        var service = CreateService(
            managedPaths: ["C:/GlyphStash/fonts/BrandSans.ttf"],
            metadata: [CreateMetadata("C:/GlyphStash/fonts/BrandSans.ttf", "Brand Sans", "hash-brand")],
            activationStore: new FakeActivationStore(
            [
                new ActivationRecord(
                    "C:/GlyphStash/fonts/BrandSans.ttf",
                    "font:Brand Sans",
                    1,
                    0,
                    DateTimeOffset.UtcNow,
                    "StaleAfterRestart",
                    "RequiresUserVerification",
                    "hash-brand")
            ]));

        var fonts = await service.RescanAsync(CancellationToken.None);

        Assert.Equal(FontActivationState.NotEnabled, Assert.Single(fonts).ActivationState);
    }

    [Fact]
    public async Task RescanAsync_KeepsInstalledStateWhenInstalledFontMatchesActiveActivation()
    {
        var installed = CreateInstalledFont("Brand Sans", "C:/Windows/Fonts/BrandSans.ttf", "hash-brand");
        var service = CreateService(
            installedFonts: [installed],
            activationStore: new FakeActivationStore(
            [
                new ActivationRecord(
                    "C:/Windows/Fonts/BrandSans.ttf",
                    "font:Brand Sans",
                    1,
                    0,
                    DateTimeOffset.UtcNow,
                    "Active",
                    "",
                    "hash-brand")
            ]));

        var fonts = await service.RescanAsync(CancellationToken.None);

        Assert.Equal(FontActivationState.Installed, Assert.Single(fonts).ActivationState);
    }

    [Fact]
    public async Task RescanAsync_FiltersInstalledEnumerationForActiveManagedTemporaryFont()
    {
        var service = CreateService(
            installedFonts: [CreateEnumerationOnlyInstalledFont("Brand Sans")],
            managedPaths: ["C:/GlyphStash/fonts/BrandSans.ttf"],
            metadata: [CreateMetadata("C:/GlyphStash/fonts/BrandSans.ttf", "Brand Sans", "hash-brand")],
            activationStore: new FakeActivationStore(
            [
                new ActivationRecord(
                    "C:/GlyphStash/fonts/BrandSans.ttf",
                    "font:Brand Sans",
                    1,
                    0,
                    DateTimeOffset.UtcNow,
                    "Active",
                    "",
                    "hash-brand")
            ]));

        var fonts = await service.RescanAsync(CancellationToken.None);

        var font = Assert.Single(fonts);
        Assert.Equal(FontSourceKind.GlyphStashManaged, font.SourceKind);
        Assert.Equal(FontActivationState.TemporarilyEnabled, font.ActivationState);
        Assert.Equal("C:/GlyphStash/fonts/BrandSans.ttf", font.PrimaryFilePath);
        Assert.DoesNotContain(font.Faces, face => face.File.Path.StartsWith("installed://", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RescanAsync_AutomaticallyAddsOnlySafeCjkTag()
    {
        var service = CreateService(
            installedFonts:
            [
                CreateInstalledFont("Noto Sans CJK SC", "C:/Windows/Fonts/NotoSansCJKsc.otf", "hash-cjk"),
                CreateInstalledFont("Commercial Sans", "C:/Windows/Fonts/CommercialSans.otf", "hash-commercial")
            ]);

        var fonts = await service.RescanAsync(CancellationToken.None);

        Assert.Contains("中文", fonts.Single(font => font.FamilyName == "Noto Sans CJK SC").Tags);
        Assert.DoesNotContain("可商用", fonts.SelectMany(font => font.Tags));
    }

    [Fact]
    public async Task RescanAsync_KeepsPhysicalInstalledFontWhenManagedTemporaryFontHasSameFamily()
    {
        var service = CreateService(
            installedFonts: [CreateInstalledFont("Brand Sans", "C:/Windows/Fonts/BrandSans.ttf", "hash-installed")],
            managedPaths: ["C:/GlyphStash/fonts/BrandSans.ttf"],
            metadata: [CreateMetadata("C:/GlyphStash/fonts/BrandSans.ttf", "Brand Sans", "hash-managed")],
            activationStore: new FakeActivationStore(
            [
                new ActivationRecord(
                    "C:/GlyphStash/fonts/BrandSans.ttf",
                    "font:Brand Sans",
                    1,
                    0,
                    DateTimeOffset.UtcNow,
                    "Active",
                    "",
                    "hash-managed")
            ]));

        var fonts = await service.RescanAsync(CancellationToken.None);

        var font = Assert.Single(fonts);
        Assert.Equal(FontSourceKind.UserInstalled, font.SourceKind);
        Assert.Equal(FontActivationState.Installed, font.ActivationState);
        Assert.Contains(font.Faces, face => string.Equals(face.File.Path, "C:/Windows/Fonts/BrandSans.ttf", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(font.Faces, face => string.Equals(face.File.Path, "C:/GlyphStash/fonts/BrandSans.ttf", StringComparison.OrdinalIgnoreCase));
    }

    private static FontLibraryService CreateService(
        IReadOnlyList<FontFamilyRecord>? installedFonts = null,
        FakeMetadataStore? store = null,
        IReadOnlyList<string>? managedPaths = null,
        IReadOnlyList<FontMetadata>? metadata = null,
        FakeActivationStore? activationStore = null)
    {
        return new FontLibraryService(
            new FakeInventory(installedFonts ?? []),
            store ?? new FakeMetadataStore(),
            new FakeSettingsStore("C:/GlyphStash/fonts"),
            new FakeManagedFontFileStore(managedPaths ?? []),
            new FakeMetadataReader(metadata ?? []),
            activationStore ?? new FakeActivationStore([]));
    }

    private static FontMetadata CreateMetadata(string path, string familyName, string sha256) =>
        new(path, "TTF", familyName, "Regular", $"{familyName} Regular", $"{familyName.Replace(' ', '-')}-Regular", null, null, null, sha256);

    private static FontFamilyRecord CreateInstalledFont(string familyName, string path, string sha256)
    {
        var file = new FontFileRecord(path, "TTF", sha256, FontSourceKind.UserInstalled, DateTimeOffset.UtcNow);
        return new FontFamilyRecord(
            familyName,
            [new FontFaceRecord(familyName, "Regular", $"{familyName} Regular", $"{familyName}-Regular", 400, "Normal", "Normal", file)],
            FontSourceKind.UserInstalled,
            FontActivationState.Installed,
            LicenseStatus.Unknown,
            "未知授权",
            [],
            [],
            false);
    }

    private static FontFamilyRecord CreateEnumerationOnlyInstalledFont(string familyName)
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

    private sealed class FakeInventory : IFontInventoryService
    {
        private readonly IReadOnlyList<FontFamilyRecord> _fonts;

        public FakeInventory(IReadOnlyList<FontFamilyRecord> fonts)
        {
            _fonts = fonts;
        }

        public Task<IReadOnlyList<FontFamilyRecord>> ScanInstalledFontsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_fonts);
    }

    private sealed class FakeMetadataStore : IFontMetadataStore
    {
        public IReadOnlyList<FontFamilyRecord>? SavedFonts { get; private set; }

        public IReadOnlyList<FontFamilyRecord>? SearchResultOverride { get; init; }

        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SaveFontIndexAsync(IReadOnlyList<FontFamilyRecord> fonts, CancellationToken cancellationToken)
        {
            SavedFonts = fonts;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<FontFamilyRecord>> SearchAsync(FontSearchQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(SearchResultOverride ?? SavedFonts ?? []);
    }

    private sealed class FakeSettingsStore : IAppSettingsStore
    {
        private readonly string _managedDirectory;

        public FakeSettingsStore(string managedDirectory)
        {
            _managedDirectory = managedDirectory;
        }

        public Task<UserFontSettings?> GetSettingsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<UserFontSettings?>(new UserFontSettings(_managedDirectory));

        public Task SaveSettingsAsync(UserFontSettings settings, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeManagedFontFileStore : IManagedFontFileStore
    {
        private readonly IReadOnlyList<string> _paths;

        public FakeManagedFontFileStore(IReadOnlyList<string> paths)
        {
            _paths = paths;
        }

        public Task<ManagedFontCopyResult> CopyToManagedDirectoryAsync(string sourcePath, UserFontSettings settings, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<string>> EnumerateManagedFontFilesAsync(UserFontSettings settings, CancellationToken cancellationToken) =>
            Task.FromResult(_paths);
    }

    private sealed class FakeMetadataReader : IFontMetadataReader
    {
        private readonly Dictionary<string, FontMetadata> _metadata;

        public FakeMetadataReader(IReadOnlyList<FontMetadata> metadata)
        {
            _metadata = metadata.ToDictionary(item => item.SourcePath, StringComparer.OrdinalIgnoreCase);
        }

        public Task<FontMetadata> ReadMetadataAsync(string fontFilePath, CancellationToken cancellationToken) =>
            Task.FromResult(_metadata[fontFilePath]);
    }

    private sealed class FakeActivationStore : IActivationStore
    {
        private readonly IReadOnlyList<ActivationRecord> _records;

        public FakeActivationStore(IReadOnlyList<ActivationRecord> records)
        {
            _records = records;
        }

        public bool RemovedAny { get; private set; }

        public bool MarkedStale { get; private set; }

        public Task<IReadOnlyList<ActivationRecord>> GetOwnedActivationsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ActivationRecord>>(_records.Where(record => record.LastKnownState == "Active").ToList());

        public Task UpsertActivationAsync(ActivationRecord record, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RemoveActivationAsync(string fontPath, string ownerKey, CancellationToken cancellationToken)
        {
            RemovedAny = true;
            return Task.CompletedTask;
        }

        public Task MarkAllOwnedStaleAsync(CancellationToken cancellationToken)
        {
            MarkedStale = true;
            return Task.CompletedTask;
        }
    }
}
