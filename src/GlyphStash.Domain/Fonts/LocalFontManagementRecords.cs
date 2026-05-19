namespace GlyphStash.Domain.Fonts;

public sealed record FontFileRef(
    string Path,
    string Format = "",
    string? Sha256 = null)
{
    public bool HasPhysicalPath => !string.IsNullOrWhiteSpace(Path) && !Path.Contains("://", StringComparison.Ordinal);
}

public sealed record ManagedFontRecord(
    string FamilyName,
    string ManagedFilePath,
    string Format,
    string? Sha256,
    string? InstalledFilePath,
    FontActivationState ActivationState,
    DateTimeOffset ImportedAt,
    DateTimeOffset? InstalledAt)
{
    public FontFileRef ToFileRef() => new(ManagedFilePath, Format, Sha256);
}

public sealed record TagRecord(
    string Name,
    int FontCount = 0);

public sealed record FontCollectionRecord(
    string Name,
    IReadOnlyList<string> FamilyNames,
    int TemporarilyEnabledCount = 0,
    int UnknownLicenseCount = 0,
    DateTimeOffset? LastExportedAt = null)
{
    public int FontCount => FamilyNames.Count;
}

public sealed record ActivationRecord(
    string FontPath,
    string OwnerKey,
    int ReferenceCount,
    int PlatformFlags,
    DateTimeOffset ActivatedAt,
    string LastKnownState,
    string CleanupStatus,
    string? Sha256 = null,
    string Scope = "CurrentUserSession");

public sealed record OperationLogEntry(
    DateTimeOffset Timestamp,
    string Category,
    string Action,
    string Message,
    string? Target = null,
    bool Succeeded = true);

public sealed record UserFontSettings(
    string ManagedFontDirectory,
    string GoogleFontsApiKey = "");
