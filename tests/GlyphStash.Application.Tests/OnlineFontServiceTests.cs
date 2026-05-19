using GlyphStash.Application.Abstractions.Fonts;
using GlyphStash.Application.Abstractions.Storage;
using GlyphStash.Application.Fonts;
using GlyphStash.Domain.Fonts;

namespace GlyphStash.Application.Tests;

public sealed class OnlineFontServiceTests
{
    [Fact]
    public async Task DownloadAsync_MapsGoogleFontsVariantToReadableFaceStyle()
    {
        var mutationStore = new CapturingMutationStore();
        var logStore = new NoopOperationLogStore();
        var family = new RemoteFontFamily(
            "google-fonts",
            "Noto Sans",
            "sans-serif",
            ["latin"],
            "v1",
            null,
            "https://fonts.google.com/specimen/Noto+Sans",
            "请查看来源页面：https://fonts.google.com/specimen/Noto+Sans",
            [new RemoteFontStyle("700italic", "700italic.ttf", "https://fonts.gstatic.com/s/notosans/700italic.ttf")]);
        var service = new OnlineFontService(
            new StubFontSourceProvider(),
            new StubSettingsStore(),
            new StubManagedFontFileStore(),
            mutationStore,
            new NoopInstallService(),
            new FontActivationCoordinator(new NoopTemporaryActivationService(), new NoopActivationStore(), logStore),
            logStore,
            new NoopDownloadRecordStore());

        await service.DownloadAsync(family, family.Styles, new OnlineFontImportOptions([], [], false, false, false), CancellationToken.None);

        Assert.NotNull(mutationStore.Family);
        var face = Assert.Single(mutationStore.Family!.Faces);
        Assert.Equal("Bold Italic 700", face.SubfamilyName);
        Assert.Equal("Noto Sans Bold Italic 700", face.FullName);
        Assert.Equal(700, face.Weight);
        Assert.Equal("Italic", face.Slant);
        Assert.Equal("C:/GlyphStash/NotoSans-700italic.ttf", face.File.Path.Replace('\\', '/'));
        Assert.Equal("managed-hash", face.File.Sha256);
    }

    private sealed class StubFontSourceProvider : IFontSourceProvider
    {
        public string ProviderId => "google-fonts";

        public Task<IReadOnlyList<RemoteFontFamily>> SearchAsync(RemoteFontSearchQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RemoteFontFamily>>([]);

        public Task<RemoteFontDownloadResult> DownloadAsync(RemoteFontDownloadRequest request, CancellationToken cancellationToken)
        {
            var style = Assert.Single(request.Styles);
            return Task.FromResult(new RemoteFontDownloadResult(
                request.Family,
                [new RemoteFontDownloadedFile(style, "C:/GlyphStash/NotoSans-700italic.ttf", "hash", "TTF")],
                "downloaded"));
        }
    }

    private sealed class StubSettingsStore : IAppSettingsStore
    {
        public Task<UserFontSettings?> GetSettingsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<UserFontSettings?>(new UserFontSettings("C:/GlyphStash", "api-key"));

        public Task SaveSettingsAsync(UserFontSettings settings, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StubManagedFontFileStore : IManagedFontFileStore
    {
        public Task<ManagedFontCopyResult> CopyToManagedDirectoryAsync(string sourcePath, UserFontSettings settings, CancellationToken cancellationToken) =>
            Task.FromResult(new ManagedFontCopyResult(Path.Combine(settings.ManagedFontDirectory, Path.GetFileName(sourcePath)), "managed-hash", false));

        public Task<IReadOnlyList<string>> EnumerateManagedFontFilesAsync(UserFontSettings settings, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }

    private sealed class CapturingMutationStore : IFontLibraryMutationStore
    {
        public FontFamilyRecord? Family { get; private set; }

        public Task SetFavoriteAsync(string familyName, bool isFavorite, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SetTagsAsync(string familyName, IReadOnlyList<string> tags, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SetCollectionsAsync(string familyName, IReadOnlyList<string> collections, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task UpsertManagedFontAsync(ManagedFontRecord managedFont, FontFamilyRecord family, CancellationToken cancellationToken)
        {
            Family = family;
            return Task.CompletedTask;
        }
    }

    private sealed class NoopInstallService : IFontInstallService
    {
        public Task<FontInstallResult> InstallForCurrentUserAsync(FontFileRef font, CancellationToken cancellationToken) =>
            Task.FromResult(new FontInstallResult(true, font.Path, null, "installed"));

        public Task<FontUninstallResult> UninstallManagedFontAsync(ManagedFontRecord managedFont, CancellationToken cancellationToken) =>
            Task.FromResult(new FontUninstallResult(true, managedFont.ManagedFilePath, "uninstalled"));
    }

    private sealed class NoopTemporaryActivationService : ITemporaryFontActivationService
    {
        public Task<ActivationResult> ActivateForCurrentUserSessionAsync(IReadOnlyList<FontFileRef> fonts, CancellationToken cancellationToken) =>
            Task.FromResult(new ActivationResult(true, [], "activated"));

        public Task<ActivationResult> DeactivateForCurrentUserSessionAsync(IReadOnlyList<FontFileRef> fonts, CancellationToken cancellationToken) =>
            Task.FromResult(new ActivationResult(true, [], "deactivated"));

        public Task DeactivateAllOwnedActivationsAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class NoopActivationStore : IActivationStore
    {
        public Task<IReadOnlyList<ActivationRecord>> GetOwnedActivationsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ActivationRecord>>([]);

        public Task UpsertActivationAsync(ActivationRecord record, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RemoveActivationAsync(string fontPath, string ownerKey, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task MarkAllOwnedStaleAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class NoopOperationLogStore : IOperationLogStore
    {
        public Task AppendOperationAsync(OperationLogEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<OperationLogEntry>> GetRecentOperationsAsync(int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<OperationLogEntry>>([]);
    }

    private sealed class NoopDownloadRecordStore : IDownloadRecordStore
    {
        public Task AddDownloadRecordAsync(DownloadRecord record, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<DownloadRecord>> GetRecentDownloadRecordsAsync(int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DownloadRecord>>([]);
    }
}
