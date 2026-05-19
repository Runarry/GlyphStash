using GlyphStash.Domain.Fonts;

namespace GlyphStash.Application.Fonts;

public sealed record FontImportPreview(
    IReadOnlyList<FontImportPreviewItem> Items)
{
    public bool HasImportableFonts => Items.Any(item => item.CanImport);
}

public sealed record FontImportPreviewItem(
    string SourcePath,
    string FileName,
    string Format,
    string FamilyName,
    string SubfamilyName,
    string FullName,
    string PostScriptName,
    string? Version,
    string? Manufacturer,
    string? LicenseText,
    string? Sha256,
    bool CanImport,
    bool CanInstall,
    bool CanTemporarilyActivate,
    string Status,
    string? ErrorMessage = null,
    int Weight = 400,
    string Width = "Normal",
    string Slant = "Normal");

public sealed record FontImportOptions(
    bool InstallForCurrentUser,
    bool TemporarilyActivate,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Collections);

public sealed record FontImportResult(
    IReadOnlyList<FontImportResultItem> Items)
{
    public int ImportedCount => Items.Count(item => item.Imported);
    public int FailedCount => Items.Count(item => !item.Imported);
}

public sealed record FontImportResultItem(
    FontImportPreviewItem Preview,
    bool Imported,
    string? ManagedPath,
    string Message);

public sealed record FontInstallResult(
    bool Succeeded,
    string FontPath,
    string? InstalledPath,
    string Message);

public sealed record FontUninstallResult(
    bool Succeeded,
    string FontPath,
    string Message);

public sealed record ActivationResult(
    bool Succeeded,
    IReadOnlyList<FontActivationFileResult> Files,
    string Message);

public sealed record FontActivationFileResult(
    FontFileRef FontFile,
    bool Succeeded,
    int LoadedCount,
    int PlatformFlags,
    string? ErrorMessage = null);

public sealed record ManagedFontCopyResult(
    string ManagedPath,
    string Sha256,
    bool AlreadyExists);

public sealed record FontMetadata(
    string SourcePath,
    string Format,
    string FamilyName,
    string SubfamilyName,
    string FullName,
    string PostScriptName,
    string? Version,
    string? Manufacturer,
    string? LicenseText,
    string Sha256,
    int Weight = 400,
    string Width = "Normal",
    string Slant = "Normal");
