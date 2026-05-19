namespace GlyphStash.Domain.Fonts;

public sealed record GlyphQuery(
    string FontFilePath,
    string FaceName,
    string SearchText = "",
    string UnicodeBlockName = "全部区块",
    bool IncludeUnmapped = false,
    int PageNumber = 1,
    int PageSize = 120);

public sealed record GlyphPage(
    IReadOnlyList<GlyphRecord> Glyphs,
    IReadOnlyList<UnicodeBlockOption> Blocks,
    int PageNumber,
    int PageSize,
    int TotalCount,
    string EmptyMessage = "")
{
    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed record GlyphRecord(
    string Character,
    int CodePoint,
    int GlyphId,
    string GlyphName,
    string FaceName,
    bool HasUnicodeMapping)
{
    public string UnicodeLabel => $"U+{CodePoint:X4}";
}

public sealed record UnicodeBlockOption(
    string Name,
    int Start,
    int End,
    int Count = 0);
