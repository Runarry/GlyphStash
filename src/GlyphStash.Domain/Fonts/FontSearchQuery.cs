namespace GlyphStash.Domain.Fonts;

public sealed record FontSearchQuery(
    string SearchText = "",
    FontSourceKind? SourceKind = null,
    FontActivationState? ActivationState = null,
    string? Tag = null,
    string? Collection = null,
    bool FavoritesOnly = false);
