using GlyphStash.Domain.Fonts;
using GlyphStash.Localization;

namespace GlyphStash.Presentation.ViewModels;

internal static class FontMergeResultTextFormatter
{
    public static string FormatSummary(FontMergeResult result) =>
        result.Succeeded
            ? AppText.FormatLiteral(
                "导出成功：跳过重复码位 {0:N0} 个，覆盖 {1:N0} 个，合并 {2:N0} 个字形。",
                "Export succeeded: skipped {0:N0} duplicate code points, overwritten {1:N0}, merged {2:N0} glyphs.",
                result.Report.SkippedDuplicateCodePointCount,
                result.Report.OverwrittenCodePointCount,
                result.Report.MergedCodePointCount)
            : AppText.FormatLiteral("导出失败：{0}", "Export failed: {0}", result.Report.ErrorMessage);
}
