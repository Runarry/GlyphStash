namespace GlyphStash.Domain.Fonts;

public sealed record FontFileRecord(
    string Path,
    string Format,
    string? Sha256,
    FontSourceKind SourceKind,
    DateTimeOffset LastSeenAt);
