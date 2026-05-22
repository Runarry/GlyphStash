using GlyphStash.Application.Abstractions.Fonts;
using GlyphStash.Application.Abstractions.Storage;
using GlyphStash.Application.Fonts;
using GlyphStash.Domain.Fonts;
using GlyphStash.Presentation.Services;
using GlyphStash.Presentation.ViewModels;

namespace GlyphStash.Presentation.Tests;

internal static class ShellViewModelTestFactory
{
    public static ShellViewModel Create(
        IFontLibraryService fontLibraryService,
        ILocalFontManagementService? localManagementService = null,
        IUserFileDialogService? fileDialogService = null,
        IOnlineFontService? onlineFontService = null,
        IGlyphCatalogService? glyphCatalogService = null,
        IUserClipboardService? clipboardService = null,
        IFontPreviewRegistry? fontPreviewRegistry = null,
        IFontMergeService? fontMergeService = null,
        IAppLocalizationService? localizationService = null) =>
        new(
            fontLibraryService,
            localManagementService ?? NoOpLocalFontManagementService.Instance,
            fileDialogService ?? NullUserFileDialogService.Instance,
            onlineFontService ?? NoOpOnlineFontService.Instance,
            glyphCatalogService ?? NoOpGlyphCatalogService.Instance,
            clipboardService ?? NullUserFileDialogService.Instance,
            fontPreviewRegistry ?? NullFontPreviewRegistry.Instance,
            fontMergeService ?? NoOpFontMergeService.Instance,
            localizationService ?? new AppLocalizationService());

    public static FontLibraryService CreateLibraryService(
        IFontInventoryService inventoryService,
        IFontMetadataStore metadataStore) =>
        new(
            inventoryService,
            metadataStore,
            NoOpSettingsStore.Instance,
            NoOpManagedFontFileStore.Instance,
            NoOpFontMetadataReader.Instance,
            NoOpActivationStore.Instance);

    private sealed class NoOpLocalFontManagementService : ILocalFontManagementService
    {
        public static NoOpLocalFontManagementService Instance { get; } = new();

        public Task<UserFontSettings?> GetSettingsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<UserFontSettings?>(new UserFontSettings(""));

        public Task SaveManagedDirectoryAsync(string directory, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SaveGoogleFontsApiKeyAsync(string apiKey, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SaveUiCultureAsync(string cultureCode, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<FontImportPreview> PreviewImportAsync(IReadOnlyList<string> sourcePaths, CancellationToken cancellationToken) =>
            Task.FromResult(new FontImportPreview([]));

        public Task<FontImportResult> ImportAsync(FontImportPreview preview, FontImportOptions options, CancellationToken cancellationToken) =>
            Task.FromResult(new FontImportResult([]));

        public Task SetFavoriteAsync(string familyName, bool isFavorite, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SetTagsAsync(string familyName, IReadOnlyList<string> tags, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<TagRecord>> GetTagsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TagRecord>>([]);

        public Task CreateTagAsync(string name, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RenameTagAsync(string oldName, string newName, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteTagAsync(string name, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SetCollectionsAsync(string familyName, IReadOnlyList<string> collections, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<FontCollectionRecord>> GetCollectionsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FontCollectionRecord>>([]);

        public Task CreateCollectionAsync(string name, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RenameCollectionAsync(string oldName, string newName, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteCollectionAsync(string name, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task AddFontToCollectionAsync(string collectionName, string familyName, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RemoveFontFromCollectionAsync(string collectionName, string familyName, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<ActivationResult> ActivateFontAsync(string ownerKey, FontFamilyRecord font, CancellationToken cancellationToken) =>
            Task.FromResult(new ActivationResult(true, [], "activated"));

        public Task<ActivationResult> DeactivateFontAsync(string ownerKey, FontFamilyRecord font, CancellationToken cancellationToken) =>
            Task.FromResult(new ActivationResult(true, [], "deactivated"));

        public Task RecordTemporaryActivationSkippedAsync(string ownerKey, FontFamilyRecord font, string reason, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<FontInstallResult> InstallFontAsync(FontFamilyRecord font, CancellationToken cancellationToken) =>
            Task.FromResult(new FontInstallResult(true, font.PrimaryFilePath, font.PrimaryFilePath, "installed"));

        public Task<FontUninstallResult> UninstallManagedFontAsync(FontFamilyRecord font, CancellationToken cancellationToken) =>
            Task.FromResult(new FontUninstallResult(true, font.PrimaryFilePath, "uninstalled"));

        public Task<IReadOnlyList<OperationLogEntry>> GetRecentOperationsAsync(int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<OperationLogEntry>>([]);
    }

    private sealed class NoOpOnlineFontService : IOnlineFontService
    {
        public static NoOpOnlineFontService Instance { get; } = new();

        public Task<IReadOnlyList<RemoteFontFamily>> SearchAsync(
            string searchText,
            string subset,
            string category,
            IReadOnlyList<string> capabilities,
            string sort,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RemoteFontFamily>>([]);

        public Task<IReadOnlyList<RemoteFontFamily>> SearchAsync(string searchText, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RemoteFontFamily>>([]);

        public Task<RemoteFontDownloadResult> DownloadAsync(
            RemoteFontFamily family,
            IReadOnlyList<RemoteFontStyle> styles,
            OnlineFontImportOptions options,
            CancellationToken cancellationToken) =>
            Task.FromResult(new RemoteFontDownloadResult(family, [], ""));
    }

    private sealed class NoOpGlyphCatalogService : IGlyphCatalogService
    {
        public static NoOpGlyphCatalogService Instance { get; } = new();

        public Task<GlyphPage> GetGlyphsAsync(GlyphQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new GlyphPage([], [new UnicodeBlockOption("全部区块", 0, 0)], 1, 1, 0, ""));

        public Task<GlyphCoverage> GetCoverageAsync(GlyphCoverageQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new GlyphCoverage([], [], [], 0, ""));
    }

    private sealed class NoOpFontMergeService : IFontMergeService
    {
        public static NoOpFontMergeService Instance { get; } = new();

        public Task<FontMergePreview> PreviewAsync(FontMergeRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new FontMergePreview([], [], [], 0, 0, 0, 0, 0));

        public Task<FontMergeResult> MergeAsync(
            FontMergeRequest request,
            IProgress<FontMergeProgress>? progress,
            CancellationToken cancellationToken)
        {
            var report = new FontMergeReport(false, "", "", "", [], 0, 0, 0, 0, 0, 0, null, DateTimeOffset.UtcNow, "", [], request.MergeMode);
            return Task.FromResult(new FontMergeResult(false, "", "", report, []));
        }

        public Task<FontMergeReport> ReadReportAsync(string reportPath, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No merge report is configured for this test.");
    }

    private sealed class NoOpSettingsStore : IAppSettingsStore
    {
        public static NoOpSettingsStore Instance { get; } = new();

        public Task<UserFontSettings?> GetSettingsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<UserFontSettings?>(new UserFontSettings(""));

        public Task SaveSettingsAsync(UserFontSettings settings, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class NoOpManagedFontFileStore : IManagedFontFileStore
    {
        public static NoOpManagedFontFileStore Instance { get; } = new();

        public Task<ManagedFontCopyResult> CopyToManagedDirectoryAsync(string sourcePath, UserFontSettings settings, CancellationToken cancellationToken) =>
            Task.FromResult(new ManagedFontCopyResult(sourcePath, "", false));

        public Task<IReadOnlyList<string>> EnumerateManagedFontFilesAsync(UserFontSettings settings, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }

    private sealed class NoOpFontMetadataReader : IFontMetadataReader
    {
        public static NoOpFontMetadataReader Instance { get; } = new();

        public Task<FontMetadata> ReadMetadataAsync(string fontFilePath, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No metadata reader is configured for this test.");
    }

    private sealed class NoOpActivationStore : IActivationStore
    {
        public static NoOpActivationStore Instance { get; } = new();

        public Task<IReadOnlyList<ActivationRecord>> GetOwnedActivationsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ActivationRecord>>([]);

        public Task UpsertActivationAsync(ActivationRecord record, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RemoveActivationAsync(string fontPath, string ownerKey, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task MarkAllOwnedStaleAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
