using GlyphStash.Application.Fonts;
using GlyphStash.Domain.Fonts;

namespace GlyphStash.Application.Abstractions.Fonts;

public interface IFontInstallService
{
    Task<FontInstallResult> InstallForCurrentUserAsync(FontFileRef fontFile, CancellationToken cancellationToken);

    Task<FontUninstallResult> UninstallManagedFontAsync(ManagedFontRecord managedFont, CancellationToken cancellationToken);
}

public interface ITemporaryFontActivationService
{
    Task<ActivationResult> ActivateForCurrentUserSessionAsync(IReadOnlyList<FontFileRef> fonts, CancellationToken cancellationToken);

    Task<ActivationResult> DeactivateForCurrentUserSessionAsync(IReadOnlyList<FontFileRef> fonts, CancellationToken cancellationToken);

    Task DeactivateAllOwnedActivationsAsync(CancellationToken cancellationToken);
}

public interface IFontMetadataReader
{
    Task<FontMetadata> ReadMetadataAsync(string fontFilePath, CancellationToken cancellationToken);
}

public interface IManagedFontFileStore
{
    Task<ManagedFontCopyResult> CopyToManagedDirectoryAsync(string sourcePath, UserFontSettings settings, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> EnumerateManagedFontFilesAsync(UserFontSettings settings, CancellationToken cancellationToken);
}

public interface IGlyphCatalogService
{
    Task<GlyphPage> GetGlyphsAsync(GlyphQuery query, CancellationToken cancellationToken);

    Task<GlyphCoverage> GetCoverageAsync(GlyphCoverageQuery query, CancellationToken cancellationToken);
}

public interface IFontSourceProvider
{
    string ProviderId { get; }

    Task<IReadOnlyList<RemoteFontFamily>> SearchAsync(RemoteFontSearchQuery query, CancellationToken cancellationToken);

    Task<RemoteFontDownloadResult> DownloadAsync(RemoteFontDownloadRequest request, CancellationToken cancellationToken);
}
