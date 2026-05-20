using GlyphStash.Domain.Fonts;

namespace GlyphStash.Application.Fonts;

public sealed record FontMergeWorkerRequest(
    string BaseFontPath,
    string SupplementalFontPath,
    IReadOnlyList<UnicodeRange> Ranges,
    string OutputPath,
    string OutputFamilyName,
    FontMergeMode MergeMode = FontMergeMode.Supplement);

public sealed record FontMergeWorkerPreviewResult(
    IReadOnlyList<FontMergeIssue> Issues,
    IReadOnlyList<FontMergeConflictItem> Conflicts,
    int RequestedCodePointCount,
    int SupplementalCoverageCount,
    int MergeCodePointCount,
    int DuplicateCodePointCount,
    int MissingCodePointCount,
    int OverwrittenCodePointCount = 0);

public sealed record FontMergeWorkerMergeResult(
    FontMergeWorkerPreviewResult Preview,
    string OutputPath);
