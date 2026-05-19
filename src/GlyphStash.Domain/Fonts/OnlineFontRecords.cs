namespace GlyphStash.Domain.Fonts;

public sealed record RemoteFontSearchQuery(
    string SearchText,
    string ApiKey);

public sealed record RemoteFontFamily(
    string ProviderId,
    string FamilyName,
    string Category,
    IReadOnlyList<string> Subsets,
    string Version,
    DateOnly? LastModified,
    string SourceUrl,
    string LicenseText,
    IReadOnlyList<RemoteFontStyle> Styles);

public sealed record RemoteFontStyle(
    string Variant,
    string FileName,
    string DownloadUrl,
    bool IsSelected = true);

public sealed record RemoteFontDownloadRequest(
    RemoteFontFamily Family,
    IReadOnlyList<RemoteFontStyle> Styles,
    string ManagedFontDirectory,
    string ApiKey);

public sealed record RemoteFontDownloadResult(
    RemoteFontFamily Family,
    IReadOnlyList<RemoteFontDownloadedFile> Files,
    string Message)
{
    public bool Succeeded => Files.Count > 0;
}

public sealed record RemoteFontDownloadedFile(
    RemoteFontStyle Style,
    string LocalPath,
    string Sha256,
    string Format);

public sealed record DownloadRecord(
    string ProviderId,
    string RemoteId,
    string FamilyName,
    string Variant,
    string DownloadUrl,
    string SourceUrl,
    string LicenseText,
    string LocalFilePath,
    DateTimeOffset DownloadedAt);
