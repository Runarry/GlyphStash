using GlyphStash.Application.Abstractions.Fonts;
using GlyphStash.Application.Abstractions.Storage;
using GlyphStash.Application.Fonts;
using GlyphStash.Domain.Fonts;

namespace GlyphStash.Application.Tests;

public sealed class LocalFontManagementServiceTests
{
    [Fact]
    public async Task Facade_DelegatesSettingsCalls()
    {
        var settingsStore = new FakeSettingsStore();
        var service = CreateService(new FakePlatformActivation(), settingsStore: settingsStore);

        await service.SaveGoogleFontsApiKeyAsync("  key  ", CancellationToken.None);

        Assert.Equal("key", settingsStore.Settings.GoogleFontsApiKey);
    }

    [Fact]
    public async Task ActivateFontAsync_WhenFontIsInstalled_DoesNotCallPlatformActivation()
    {
        var platform = new FakePlatformActivation();
        var service = CreateService(platform);
        var font = CreateFont("Installed Sans", FontActivationState.Installed, FontSourceKind.UserInstalled);

        var result = await service.ActivateFontAsync("font:Installed Sans", font, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(0, platform.ActivateCalls);
        Assert.Contains("已安装", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportAsync_WhenInstallSelected_IgnoresTemporaryActivation()
    {
        var platform = new FakePlatformActivation();
        var mutationStore = new FakeMutationStore();
        var service = CreateService(platform, mutationStore: mutationStore);
        var preview = new FontImportPreview(
        [
            new FontImportPreviewItem(
                "C:/Incoming/BrandSans.ttf",
                "BrandSans.ttf",
                "TTF",
                "Brand Sans",
                "Regular",
                "Brand Sans Regular",
                "BrandSans-Regular",
                null,
                null,
                null,
                "hash",
                true,
                true,
                true,
                "可导入")
        ]);

        var result = await service.ImportAsync(
            preview,
            new FontImportOptions(true, true, [], []),
            CancellationToken.None);

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(0, platform.ActivateCalls);
        Assert.Equal(FontActivationState.Installed, mutationStore.ManagedFont?.ActivationState);
        Assert.Equal(FontActivationState.Installed, mutationStore.Family?.ActivationState);
    }

    [Fact]
    public async Task TagAndCollectionManagement_WritesThroughAndLogs()
    {
        var platform = new FakePlatformActivation();
        var tagStore = new FakeTagStore();
        var collectionStore = new FakeCollectionStore();
        var logStore = new FakeOperationLogStore();
        var service = CreateService(platform, tagStore: tagStore, collectionStore: collectionStore, logStore: logStore);

        await service.CreateTagAsync("中文", CancellationToken.None);
        await service.RenameTagAsync("中文", "CJK", CancellationToken.None);
        await service.DeleteTagAsync("CJK", CancellationToken.None);
        await service.CreateCollectionAsync("官网改版", CancellationToken.None);
        await service.RenameCollectionAsync("官网改版", "品牌官网", CancellationToken.None);
        await service.DeleteCollectionAsync("品牌官网", CancellationToken.None);

        Assert.Equal(["create:中文", "rename:中文->CJK", "delete:CJK"], tagStore.Operations);
        Assert.Equal(["create:官网改版", "rename:官网改版->品牌官网", "delete:品牌官网"], collectionStore.Operations);
        Assert.Contains(logStore.Entries, entry => entry.Category == "tags" && entry.Action == "delete");
        Assert.Contains(logStore.Entries, entry => entry.Category == "collection" && entry.Action == "delete");
    }

    private static LocalFontManagementService CreateService(
        FakePlatformActivation platform,
        FakeMutationStore? mutationStore = null,
        FakeTagStore? tagStore = null,
        FakeCollectionStore? collectionStore = null,
        FakeOperationLogStore? logStore = null,
        FakeSettingsStore? settingsStore = null)
    {
        logStore ??= new FakeOperationLogStore();
        var operationLogger = new OperationLogger(logStore);
        settingsStore ??= new FakeSettingsStore();
        mutationStore ??= new FakeMutationStore();
        tagStore ??= new FakeTagStore();
        collectionStore ??= new FakeCollectionStore();
        var installService = new FakeInstallService();
        var activationCoordinator = new FontActivationCoordinator(platform, new FakeActivationStore(), logStore);
        return new LocalFontManagementService(
            new LocalFontSettingsService(settingsStore, operationLogger),
            new LocalFontImportService(
                new FakeMetadataReader(),
                new FakeManagedFontFileStore(),
                settingsStore,
                new FakeMetadataStore(),
                mutationStore,
                collectionStore,
                installService,
                activationCoordinator,
                operationLogger),
            new LocalFontOrganizationService(mutationStore, tagStore, collectionStore, operationLogger),
            new LocalFontActivationService(installService, activationCoordinator, operationLogger),
            new LocalFontOperationLogService(logStore));
    }

    private static FontFamilyRecord CreateFont(string family, FontActivationState state, FontSourceKind sourceKind)
    {
        var file = new FontFileRecord($"C:/Fonts/{family}.ttf", "TTF", "hash", sourceKind, DateTimeOffset.UtcNow);
        return new FontFamilyRecord(
            family,
            [new FontFaceRecord(family, "Regular", $"{family} Regular", $"{family}-Regular", 400, "Normal", "Normal", file)],
            sourceKind,
            state,
            LicenseStatus.Unknown,
            "未知授权",
            [],
            [],
            false);
    }

    private sealed class FakePlatformActivation : ITemporaryFontActivationService
    {
        public int ActivateCalls { get; private set; }

        public Task<ActivationResult> ActivateForCurrentUserSessionAsync(IReadOnlyList<FontFileRef> fonts, CancellationToken cancellationToken)
        {
            ActivateCalls += fonts.Count;
            return Task.FromResult(new ActivationResult(true, fonts.Select(font => new FontActivationFileResult(font, true, 1, 0)).ToList(), "activated"));
        }

        public Task<ActivationResult> DeactivateForCurrentUserSessionAsync(IReadOnlyList<FontFileRef> fonts, CancellationToken cancellationToken) =>
            Task.FromResult(new ActivationResult(true, [], "deactivated"));

        public Task DeactivateAllOwnedActivationsAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeActivationStore : IActivationStore
    {
        public Task<IReadOnlyList<ActivationRecord>> GetOwnedActivationsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ActivationRecord>>([]);

        public Task UpsertActivationAsync(ActivationRecord record, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RemoveActivationAsync(string fontPath, string ownerKey, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task MarkAllOwnedStaleAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeMetadataReader : IFontMetadataReader
    {
        public Task<FontMetadata> ReadMetadataAsync(string fontFilePath, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeManagedFontFileStore : IManagedFontFileStore
    {
        public Task<ManagedFontCopyResult> CopyToManagedDirectoryAsync(string sourcePath, UserFontSettings settings, CancellationToken cancellationToken) =>
            Task.FromResult(new ManagedFontCopyResult("C:/GlyphStash/BrandSans.ttf", "hash", false));

        public Task<IReadOnlyList<string>> EnumerateManagedFontFilesAsync(UserFontSettings settings, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }

    private sealed class FakeSettingsStore : IAppSettingsStore
    {
        public UserFontSettings Settings { get; private set; } = new("C:/GlyphStash");

        public Task<UserFontSettings?> GetSettingsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<UserFontSettings?>(Settings);

        public Task SaveSettingsAsync(UserFontSettings settings, CancellationToken cancellationToken)
        {
            Settings = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeMetadataStore : IFontMetadataStore
    {
        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SaveFontIndexAsync(IReadOnlyList<FontFamilyRecord> fonts, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<FontFamilyRecord>> SearchAsync(FontSearchQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FontFamilyRecord>>([]);
    }

    private sealed class FakeMutationStore : IFontLibraryMutationStore
    {
        public ManagedFontRecord? ManagedFont { get; private set; }

        public FontFamilyRecord? Family { get; private set; }

        public Task SetFavoriteAsync(string familyName, bool isFavorite, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SetTagsAsync(string familyName, IReadOnlyList<string> tags, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SetCollectionsAsync(string familyName, IReadOnlyList<string> collections, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task UpsertManagedFontAsync(ManagedFontRecord managedFont, FontFamilyRecord family, CancellationToken cancellationToken)
        {
            ManagedFont = managedFont;
            Family = family;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCollectionStore : ICollectionStore
    {
        public List<string> Operations { get; } = [];

        public Task<IReadOnlyList<FontCollectionRecord>> GetCollectionsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FontCollectionRecord>>([]);

        public Task CreateCollectionAsync(string name, CancellationToken cancellationToken)
        {
            Operations.Add($"create:{name}");
            return Task.CompletedTask;
        }

        public Task RenameCollectionAsync(string oldName, string newName, CancellationToken cancellationToken)
        {
            Operations.Add($"rename:{oldName}->{newName}");
            return Task.CompletedTask;
        }

        public Task DeleteCollectionAsync(string name, CancellationToken cancellationToken)
        {
            Operations.Add($"delete:{name}");
            return Task.CompletedTask;
        }

        public Task AddFontToCollectionAsync(string collectionName, string familyName, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RemoveFontFromCollectionAsync(string collectionName, string familyName, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeTagStore : ITagStore
    {
        public List<string> Operations { get; } = [];

        public Task<IReadOnlyList<TagRecord>> GetTagsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TagRecord>>([]);

        public Task CreateTagAsync(string name, CancellationToken cancellationToken)
        {
            Operations.Add($"create:{name}");
            return Task.CompletedTask;
        }

        public Task RenameTagAsync(string oldName, string newName, CancellationToken cancellationToken)
        {
            Operations.Add($"rename:{oldName}->{newName}");
            return Task.CompletedTask;
        }

        public Task DeleteTagAsync(string name, CancellationToken cancellationToken)
        {
            Operations.Add($"delete:{name}");
            return Task.CompletedTask;
        }
    }

    private sealed class FakeInstallService : IFontInstallService
    {
        public Task<FontInstallResult> InstallForCurrentUserAsync(FontFileRef fontFile, CancellationToken cancellationToken) =>
            Task.FromResult(new FontInstallResult(true, fontFile.Path, "C:/Users/Current/AppData/Local/Microsoft/Windows/Fonts/BrandSans.ttf", "installed"));

        public Task<FontUninstallResult> UninstallManagedFontAsync(ManagedFontRecord managedFont, CancellationToken cancellationToken) =>
            Task.FromResult(new FontUninstallResult(true, managedFont.ManagedFilePath, "uninstalled"));
    }

    private sealed class FakeOperationLogStore : IOperationLogStore
    {
        public List<OperationLogEntry> Entries { get; } = [];

        public Task AppendOperationAsync(OperationLogEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<OperationLogEntry>> GetRecentOperationsAsync(int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<OperationLogEntry>>(Entries.Take(limit).ToList());
    }
}
