namespace GlyphStash.Domain.Fonts;

public sealed record UnicodeRange(int Start, int End)
{
    public int Count => End - Start + 1;

    public string Label => Start == End
        ? FormatCodePoint(Start)
        : $"{FormatCodePoint(Start)}-{FormatCodePoint(End)}";

    public static string FormatCodePoint(int codePoint) => $"U+{codePoint:X4}";
}

public static class UnicodeRangeParser
{
    private static readonly char[] RangeSeparators = [',', ';', '，', '；', '\r', '\n'];

    public static IReadOnlyList<UnicodeRange> Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("请输入 Unicode 范围。");
        }

        var ranges = new List<UnicodeRange>();
        foreach (var rawPart in text.Split(RangeSeparators, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            ranges.Add(ParsePart(rawPart));
        }

        if (ranges.Count == 0)
        {
            throw new InvalidOperationException("请输入 Unicode 范围。");
        }

        return Normalize(ranges);
    }

    public static IReadOnlyList<int> Expand(IReadOnlyList<UnicodeRange> ranges)
    {
        var codePoints = new List<int>(ranges.Sum(range => range.Count));
        foreach (var range in ranges)
        {
            for (var codePoint = range.Start; codePoint <= range.End; codePoint++)
            {
                codePoints.Add(codePoint);
            }
        }

        return codePoints;
    }

    public static int CountCodePoints(IReadOnlyList<UnicodeRange> ranges) =>
        ranges.Sum(range => range.Count);

    private static UnicodeRange ParsePart(string value)
    {
        var pieces = value.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (pieces.Length == 1)
        {
            var codePoint = ParseCodePoint(pieces[0]);
            return new UnicodeRange(codePoint, codePoint);
        }

        if (pieces.Length != 2)
        {
            throw new InvalidOperationException($"Unicode 范围格式无效：{value}");
        }

        var start = ParseCodePoint(pieces[0]);
        var end = ParseCodePoint(pieces[1]);
        if (start > end)
        {
            throw new InvalidOperationException($"Unicode 范围起点不能大于终点：{value}");
        }

        EnsureNoSurrogates(start, end, value);
        return new UnicodeRange(start, end);
    }

    private static int ParseCodePoint(string value)
    {
        var normalized = value.Trim();
        if (normalized.StartsWith("U+", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[2..];
        }
        else if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[2..];
        }

        if (normalized.Length is < 1 or > 6 || normalized.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidOperationException($"Unicode 码位格式无效：{value}");
        }

        var codePoint = Convert.ToInt32(normalized, 16);
        if (codePoint is < 0 or > 0x10FFFF)
        {
            throw new InvalidOperationException($"Unicode 码位超出范围：{value}");
        }

        if (codePoint is >= 0xD800 and <= 0xDFFF)
        {
            throw new InvalidOperationException($"Unicode 码位不能位于代理区：{value}");
        }

        return codePoint;
    }

    private static void EnsureNoSurrogates(int start, int end, string original)
    {
        if (start <= 0xDFFF && end >= 0xD800)
        {
            throw new InvalidOperationException($"Unicode 范围不能包含代理区：{original}");
        }
    }

    private static IReadOnlyList<UnicodeRange> Normalize(IReadOnlyList<UnicodeRange> ranges)
    {
        var ordered = ranges
            .OrderBy(range => range.Start)
            .ThenBy(range => range.End)
            .ToList();
        var normalized = new List<UnicodeRange>();
        foreach (var range in ordered)
        {
            if (normalized.Count == 0)
            {
                normalized.Add(range);
                continue;
            }

            var current = normalized[^1];
            if (range.Start <= current.End + 1)
            {
                normalized[^1] = current with { End = Math.Max(current.End, range.End) };
                continue;
            }

            normalized.Add(range);
        }

        return normalized;
    }
}

public sealed record LicenseSnapshot(
    LicenseStatus Status,
    string Text);

public sealed record FontMergeFontSelection(
    string FamilyName,
    string StyleName,
    FontFileRef File,
    LicenseSnapshot License);

public sealed record FontMergeRequest(
    FontMergeFontSelection? BaseFont,
    FontMergeFontSelection? SupplementalFont,
    string UnicodeRanges,
    string OutputPath,
    string OutputFamilyName,
    bool LicenseConfirmed,
    DateTimeOffset? LicenseConfirmedAt = null);

public sealed record FontMergePreview(
    IReadOnlyList<UnicodeRange> Ranges,
    IReadOnlyList<FontMergeIssue> Issues,
    IReadOnlyList<FontMergeConflictItem> Conflicts,
    int RequestedCodePointCount,
    int SupplementalCoverageCount,
    int MergeCodePointCount,
    int DuplicateCodePointCount,
    int MissingCodePointCount,
    string DefaultConflictStrategy = "基础字体已有码位默认跳过")
{
    public bool HasBlockingIssues => Issues.Any(issue => issue.Severity == FontMergeIssueSeverity.Error);
}

public sealed record FontMergeIssue(
    FontMergeIssueKind Kind,
    FontMergeIssueSeverity Severity,
    string Message,
    string Target = "");

public enum FontMergeIssueKind
{
    InvalidInput = 0,
    MissingFont = 1,
    MissingFile = 2,
    UnsupportedFormat = 3,
    UnsupportedFontKind = 4,
    VariableFont = 5,
    OpenTypeLayoutConflict = 6,
    LicenseUnknown = 7,
    LicenseNotConfirmed = 8,
    OutputPathInvalid = 9,
    OutputPathExists = 10,
    WorkerUnavailable = 11,
    WorkerFailed = 12,
    Canceled = 13
}

public enum FontMergeIssueSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2
}

public sealed record FontMergeConflictItem(
    int CodePoint,
    string Character,
    FontMergeCodePointState BaseState,
    FontMergeCodePointState SupplementalState,
    FontMergeDecision DefaultDecision,
    string Note)
{
    public string UnicodeLabel => UnicodeRange.FormatCodePoint(CodePoint);
}

public enum FontMergeCodePointState
{
    Unknown = 0,
    Missing = 1,
    Present = 2
}

public enum FontMergeDecision
{
    Merge = 0,
    SkipDuplicate = 1,
    RecordMissing = 2,
    Blocked = 3
}

public sealed record FontMergeProgress(
    int Percent,
    string Stage,
    string Message);

public sealed record FontMergeReport(
    bool Succeeded,
    string OutputPath,
    string BaseFontFamily,
    string SupplementalFontFamily,
    IReadOnlyList<string> Ranges,
    int RequestedCodePointCount,
    int MergedCodePointCount,
    int SkippedDuplicateCodePointCount,
    int MissingCodePointCount,
    int ConflictCount,
    DateTimeOffset? LicenseConfirmedAt,
    DateTimeOffset FinishedAt,
    string ErrorMessage,
    IReadOnlyList<FontMergeIssue> Issues);

public sealed record FontMergeResult(
    bool Succeeded,
    string OutputPath,
    string ReportPath,
    FontMergeReport Report,
    IReadOnlyList<FontMergeIssue> Issues)
{
    public string Summary => Succeeded
        ? $"导出成功：跳过重复码位 {Report.SkippedDuplicateCodePointCount:N0} 个，合并 {Report.MergedCodePointCount:N0} 个字形。"
        : $"导出失败：{Report.ErrorMessage}";
}
