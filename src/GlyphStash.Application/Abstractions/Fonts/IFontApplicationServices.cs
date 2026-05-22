using GlyphStash.Application.Fonts;
using GlyphStash.Domain.Fonts;

namespace GlyphStash.Application.Abstractions.Fonts;

public interface IFontLibraryService
{
    Task<IReadOnlyList<FontFamilyRecord>> LoadCachedFontsAsync(FontSearchQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<FontFamilyRecord>> RescanAsync(CancellationToken cancellationToken);
}

public interface ILocalFontManagementService
{
    Task<UserFontSettings?> GetSettingsAsync(CancellationToken cancellationToken);

    Task SaveManagedDirectoryAsync(string directory, CancellationToken cancellationToken);

    Task SaveGoogleFontsApiKeyAsync(string apiKey, CancellationToken cancellationToken);

    Task SaveUiCultureAsync(string cultureCode, CancellationToken cancellationToken);

    Task<FontImportPreview> PreviewImportAsync(IReadOnlyList<string> sourcePaths, CancellationToken cancellationToken);

    Task<FontImportResult> ImportAsync(FontImportPreview preview, FontImportOptions options, CancellationToken cancellationToken);

    Task SetFavoriteAsync(string familyName, bool isFavorite, CancellationToken cancellationToken);

    Task SetTagsAsync(string familyName, IReadOnlyList<string> tags, CancellationToken cancellationToken);

    Task<IReadOnlyList<TagRecord>> GetTagsAsync(CancellationToken cancellationToken);

    Task CreateTagAsync(string name, CancellationToken cancellationToken);

    Task RenameTagAsync(string oldName, string newName, CancellationToken cancellationToken);

    Task DeleteTagAsync(string name, CancellationToken cancellationToken);

    Task SetCollectionsAsync(string familyName, IReadOnlyList<string> collections, CancellationToken cancellationToken);

    Task<IReadOnlyList<FontCollectionRecord>> GetCollectionsAsync(CancellationToken cancellationToken);

    Task CreateCollectionAsync(string name, CancellationToken cancellationToken);

    Task RenameCollectionAsync(string oldName, string newName, CancellationToken cancellationToken);

    Task DeleteCollectionAsync(string name, CancellationToken cancellationToken);

    Task AddFontToCollectionAsync(string collectionName, string familyName, CancellationToken cancellationToken);

    Task RemoveFontFromCollectionAsync(string collectionName, string familyName, CancellationToken cancellationToken);

    Task<ActivationResult> ActivateFontAsync(string ownerKey, FontFamilyRecord font, CancellationToken cancellationToken);

    Task<ActivationResult> DeactivateFontAsync(string ownerKey, FontFamilyRecord font, CancellationToken cancellationToken);

    Task RecordTemporaryActivationSkippedAsync(string ownerKey, FontFamilyRecord font, string reason, CancellationToken cancellationToken);

    Task<FontInstallResult> InstallFontAsync(FontFamilyRecord font, CancellationToken cancellationToken);

    Task<FontUninstallResult> UninstallManagedFontAsync(FontFamilyRecord font, CancellationToken cancellationToken);

    Task<IReadOnlyList<OperationLogEntry>> GetRecentOperationsAsync(int limit, CancellationToken cancellationToken);
}

public interface IOnlineFontService
{
    Task<IReadOnlyList<RemoteFontFamily>> SearchAsync(
        string searchText,
        string subset,
        string category,
        IReadOnlyList<string> capabilities,
        string sort,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RemoteFontFamily>> SearchAsync(string searchText, CancellationToken cancellationToken);

    Task<RemoteFontDownloadResult> DownloadAsync(
        RemoteFontFamily family,
        IReadOnlyList<RemoteFontStyle> styles,
        OnlineFontImportOptions options,
        CancellationToken cancellationToken);
}

public interface IFontMergeService
{
    Task<FontMergePreview> PreviewAsync(FontMergeRequest request, CancellationToken cancellationToken);

    Task<FontMergeResult> MergeAsync(
        FontMergeRequest request,
        IProgress<FontMergeProgress>? progress,
        CancellationToken cancellationToken);

    Task<FontMergeReport> ReadReportAsync(string reportPath, CancellationToken cancellationToken);
}
