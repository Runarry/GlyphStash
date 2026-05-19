using GlyphStash.Application.Abstractions.Fonts;
using GlyphStash.Application.Abstractions.Storage;
using GlyphStash.Application.Fonts;
using GlyphStash.Domain.Fonts;

namespace GlyphStash.Application.Tests;

public sealed class LocalFontManagementServiceTests
{
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

    private static LocalFontManagementService CreateService(
        FakePlatformActivation platform,
        FakeMutationStore? mutationStore = null)
    {
        var logStore = new FakeOperationLogStore();
        return new LocalFontManagementService(
            new FakeMetadataReader(),
            new FakeManagedFontFileStore(),
            new FakeSettingsStore(),
            new FakeMetadataStore(),
            mutationStore ?? new FakeMutationStore(),
            new FakeCollectionStore(),
            new FakeInstallService(),
            new FontActivationCoordinator(platform, new FakeActivationStore(), logStore),
            logStore);
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
        public Task<UserFontSettings?> GetSettingsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<UserFontSettings?>(new UserFontSettings("C:/GlyphStash"));

        public Task SaveSettingsAsync(UserFontSettings settings, CancellationToken cancellationToken) => Task.CompletedTask;
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
        public Task<IReadOnlyList<FontCollectionRecord>> GetCollectionsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FontCollectionRecord>>([]);

        public Task CreateCollectionAsync(string name, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RenameCollectionAsync(string oldName, string newName, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteCollectionAsync(string name, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task AddFontToCollectionAsync(string collectionName, string familyName, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RemoveFontFromCollectionAsync(string collectionName, string familyName, CancellationToken cancellationToken) => Task.CompletedTask;
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
        public Task AppendOperationAsync(OperationLogEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<OperationLogEntry>> GetRecentOperationsAsync(int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<OperationLogEntry>>([]);
    }
}
