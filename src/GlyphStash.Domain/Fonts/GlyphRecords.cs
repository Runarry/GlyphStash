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

public sealed record GlyphCoverageQuery(
    string FontFilePath,
    string FaceName);

public sealed record GlyphCoverage(
    IReadOnlyList<UnicodeRange> Ranges,
    IReadOnlyList<GlyphCoverageBlock> Blocks,
    IReadOnlyList<GlyphCoverageSegment> Segments,
    int TotalCodePointCount,
    string EmptyMessage = "")
{
    public bool HasCoverage => TotalCodePointCount > 0;
}

public sealed record GlyphCoverageBlock(
    string Name,
    int Start,
    int End,
    int Count,
    bool IsOther = false)
{
    public string RangeLabel => Start == 0 && End == 0 ? "" : new UnicodeRange(Start, End).Label;
}

public sealed record GlyphCoverageSegment(
    UnicodeRange Range,
    string BlockName,
    GlyphCoveragePresence Presence = GlyphCoveragePresence.Present)
{
    public string RangeLabel => Range.Label;

    public int CodePointCount => Range.Count;
}

public enum GlyphCoveragePresence
{
    Present = 0,
    BaseOnly = 1,
    SupplementalOnly = 2,
    Both = 3
}

public static class UnicodeCoverageBlocks
{
    public const string AllBlocks = "全部区块";
    public const string OtherCoverage = "其他覆盖";

    public static IReadOnlyList<UnicodeBlockOption> KnownBlocks { get; } =
    [
        new("Basic Latin", 0x0000, 0x007F),
        new("Latin-1 Supplement", 0x0080, 0x00FF),
        new("Latin Extended", 0x0100, 0x024F),
        new("Greek and Coptic", 0x0370, 0x03FF),
        new("Cyrillic", 0x0400, 0x04FF),
        new("General Punctuation", 0x2000, 0x206F),
        new("Currency Symbols", 0x20A0, 0x20CF),
        new("Letterlike Symbols", 0x2100, 0x214F),
        new("Number Forms", 0x2150, 0x218F),
        new("Arrows", 0x2190, 0x21FF),
        new("Mathematical Operators", 0x2200, 0x22FF),
        new("CJK Symbols and Punctuation", 0x3000, 0x303F),
        new("Hiragana", 0x3040, 0x309F),
        new("Katakana", 0x30A0, 0x30FF),
        new("CJK Unified Ideographs", 0x4E00, 0x9FFF),
        new("Private Use Area", 0xE000, 0xF8FF),
        new("CJK Compatibility Ideographs", 0xF900, 0xFAFF)
    ];

    public static UnicodeBlockOption? FindKnownBlock(int codePoint) =>
        KnownBlocks.FirstOrDefault(block => codePoint >= block.Start && codePoint <= block.End);

    public static bool IsKnownBlockCodePoint(int codePoint) => FindKnownBlock(codePoint) is not null;
}
