using System.Text.Json;
using GlyphStash.Application.Abstractions.Fonts;
using GlyphStash.Application.Abstractions.Storage;
using GlyphStash.Domain.Fonts;
using GlyphStash.Localization;
using static GlyphStash.Localization.AppTextExtensions;

namespace GlyphStash.Application.Fonts;

public sealed class FontMergeService : IFontMergeService
{
    private static readonly string[] SupportedExtensions = [".ttf", ".otf"];
    private static readonly string[] RecognizedButUnsupportedExtensions = [".ttc", ".otc", ".woff", ".woff2"];

    private static readonly JsonSerializerOptions ReportJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private static readonly FontMergeReportJsonContext ReportJsonContext = new(ReportJsonOptions);

    private readonly IFontMergeWorker _worker;
    private readonly IOperationLogStore? _operationLogStore;

    public FontMergeService(IFontMergeWorker worker, IOperationLogStore? operationLogStore = null)
    {
        _worker = worker;
        _operationLogStore = operationLogStore;
    }

    public async Task<FontMergePreview> PreviewAsync(FontMergeRequest request, CancellationToken cancellationToken)
    {
        var validation = ValidateRequest(request, validateOutput: false);
        if (validation.Issues.Any(issue => issue.Severity == FontMergeIssueSeverity.Error))
        {
            return EmptyPreview(validation.Ranges, validation.Issues);
        }

        try
        {
            var workerResult = await _worker.PreviewAsync(
                CreateWorkerRequest(request, validation.Ranges),
                null,
                cancellationToken).ConfigureAwait(false);

            return CreatePreview(validation.Ranges, validation.Issues.Concat(workerResult.Issues).ToList(), workerResult, request.MergeMode);
        }
        catch (OperationCanceledException)
        {
            return EmptyPreview(
                validation.Ranges,
                [.. validation.Issues, new FontMergeIssue(FontMergeIssueKind.Canceled, FontMergeIssueSeverity.Error, L("合并预览已取消。"))]);
        }
        catch (Exception ex)
        {
            return EmptyPreview(
                validation.Ranges,
                [.. validation.Issues, new FontMergeIssue(FontMergeIssueKind.WorkerFailed, FontMergeIssueSeverity.Error, ToMergeUserMessage(ex), "fontTools worker")]);
        }
    }

    public async Task<FontMergeResult> MergeAsync(
        FontMergeRequest request,
        IProgress<FontMergeProgress>? progress,
        CancellationToken cancellationToken)
    {
        var confirmedAt = request.LicenseConfirmedAt ?? DateTimeOffset.UtcNow;
        var validation = ValidateRequest(request with { LicenseConfirmedAt = confirmedAt }, validateOutput: true);
        if (validation.Issues.Any(issue => issue.Severity == FontMergeIssueSeverity.Error))
        {
            var failedReport = CreateReport(request, validation.Ranges, null, validation.Issues, false, validation.Issues.First(issue => issue.Severity == FontMergeIssueSeverity.Error).Message, "");
            await LogAsync("merge", "export", failedReport.ErrorMessage, request.OutputPath, false, cancellationToken).ConfigureAwait(false);
            return new FontMergeResult(false, request.OutputPath, "", failedReport, validation.Issues);
        }

        progress?.Report(new FontMergeProgress(4, L("准备"), L("正在启动 fontTools worker...")));
        try
        {
            var workerResult = await _worker.MergeAsync(
                CreateWorkerRequest(request, validation.Ranges),
                progress,
                cancellationToken).ConfigureAwait(false);

            var issues = validation.Issues.Concat(workerResult.Preview.Issues).ToList();
            if (issues.Any(issue => issue.Severity == FontMergeIssueSeverity.Error))
            {
                var failedReport = CreateReport(request, validation.Ranges, workerResult.Preview, issues, false, L("合并任务存在阻止级问题。"), "");
                await LogAsync("merge", "export", failedReport.ErrorMessage, request.OutputPath, false, cancellationToken).ConfigureAwait(false);
                return new FontMergeResult(false, request.OutputPath, "", failedReport, issues);
            }

            if (!File.Exists(request.OutputPath))
            {
                var missingOutput = new FontMergeIssue(FontMergeIssueKind.WorkerFailed, FontMergeIssueSeverity.Error, L("fontTools worker 未生成输出字体文件。"), request.OutputPath);
                var failedReport = CreateReport(request, validation.Ranges, workerResult.Preview, [.. validation.Issues, missingOutput], false, missingOutput.Message, "");
                await LogAsync("merge", "export", failedReport.ErrorMessage, request.OutputPath, false, cancellationToken).ConfigureAwait(false);
                return new FontMergeResult(false, request.OutputPath, "", failedReport, failedReport.Issues);
            }

            progress?.Report(new FontMergeProgress(96, L("报告"), L("正在写入合并报告...")));
            var report = CreateReport(request, validation.Ranges, workerResult.Preview, issues, true, "", request.OutputPath);
            var reportPath = await WriteReportAsync(request.OutputPath, report, cancellationToken).ConfigureAwait(false);
            await LogAsync("merge", "export", F("合并导出成功：{0}", "Merge export succeeded: {0}", request.OutputPath), request.OutputPath, true, cancellationToken).ConfigureAwait(false);
            progress?.Report(new FontMergeProgress(100, L("完成"), L("合并报告已生成。")));
            return new FontMergeResult(true, request.OutputPath, reportPath, report, issues);
        }
        catch (OperationCanceledException)
        {
            var issue = new FontMergeIssue(FontMergeIssueKind.Canceled, FontMergeIssueSeverity.Error, L("合并任务已取消。"));
            var report = CreateReport(request, validation.Ranges, null, [.. validation.Issues, issue], false, issue.Message, "");
            var reportPath = await TryWriteReportAsync(request.OutputPath, report, CancellationToken.None).ConfigureAwait(false);
            await LogAsync("merge", "export", issue.Message, request.OutputPath, false, CancellationToken.None).ConfigureAwait(false);
            return new FontMergeResult(false, request.OutputPath, reportPath, report, report.Issues);
        }
        catch (Exception ex)
        {
            var issue = new FontMergeIssue(FontMergeIssueKind.WorkerFailed, FontMergeIssueSeverity.Error, ToMergeUserMessage(ex), "fontTools worker");
            var report = CreateReport(request, validation.Ranges, null, [.. validation.Issues, issue], false, issue.Message, "");
            var reportPath = await TryWriteReportAsync(request.OutputPath, report, cancellationToken).ConfigureAwait(false);
            await LogAsync("merge", "export", issue.Message, request.OutputPath, false, cancellationToken).ConfigureAwait(false);
            return new FontMergeResult(false, request.OutputPath, reportPath, report, report.Issues);
        }
    }

    public async Task<FontMergeReport> ReadReportAsync(string reportPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reportPath) || !File.Exists(reportPath))
        {
            throw new InvalidOperationException(L("合并报告文件不存在。"));
        }

        await using var stream = File.OpenRead(reportPath);
        return await JsonSerializer.DeserializeAsync(stream, ReportJsonContext.FontMergeReport, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException(L("合并报告文件格式无效。"));
    }

    public static string BuildReportPath(string outputPath)
    {
        var directory = Path.GetDirectoryName(outputPath);
        var fileName = Path.GetFileName(outputPath);
        return Path.Combine(string.IsNullOrWhiteSpace(directory) ? "." : directory, $"{fileName}.glyphstash-merge-report.json");
    }

    private RequestValidation ValidateRequest(FontMergeRequest request, bool validateOutput)
    {
        var issues = new List<FontMergeIssue>();
        IReadOnlyList<UnicodeRange> ranges = [];
        try
        {
            ranges = UnicodeRangeParser.Parse(request.UnicodeRanges);
        }
        catch (Exception ex)
        {
            issues.Add(new FontMergeIssue(FontMergeIssueKind.InvalidInput, FontMergeIssueSeverity.Error, ToMergeUserMessage(ex), L("Unicode 范围")));
        }

        ValidateFont(request.BaseFont, L("基础字体 A"), issues);
        ValidateFont(request.SupplementalFont, L("补充字体 B"), issues);
        ValidateDistinctInputs(request, issues);
        AddLicenseIssues(request.BaseFont, L("基础字体 A"), issues);
        AddLicenseIssues(request.SupplementalFont, L("补充字体 B"), issues);

        if (validateOutput)
        {
            ValidateOutput(request, issues);
        }

        return new RequestValidation(ranges, issues);
    }

    private static void ValidateFont(FontMergeFontSelection? font, string target, List<FontMergeIssue> issues)
    {
        if (font is null)
        {
            issues.Add(new FontMergeIssue(FontMergeIssueKind.MissingFont, FontMergeIssueSeverity.Error, F("请选择{0}。", "Select {0}.", target), target));
            return;
        }

        if (!font.File.HasPhysicalPath)
        {
            issues.Add(new FontMergeIssue(FontMergeIssueKind.MissingFile, FontMergeIssueSeverity.Error, F("{0}没有可读取的本地字体文件路径。", "{0} has no readable local font file path.", target), target));
            return;
        }

        if (!File.Exists(font.File.Path))
        {
            issues.Add(new FontMergeIssue(FontMergeIssueKind.MissingFile, FontMergeIssueSeverity.Error, F("{0}文件不存在：{1}", "{0} file does not exist: {1}", target, font.File.Path), target));
            return;
        }

        var extension = Path.GetExtension(font.File.Path);
        if (RecognizedButUnsupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            issues.Add(new FontMergeIssue(FontMergeIssueKind.UnsupportedFontKind, FontMergeIssueSeverity.Error, F("{0}是 {1}，M4 v1 仅支持静态单字体 TTF/OTF；合并阶段仅支持静态 TrueType/glyf 轮廓，部分 OTF/CFF 暂不支持。", "{0} is {1}. M4 v1 only supports static single-font TTF/OTF; merging only supports static TrueType/glyf outlines, and some OTF/CFF fonts are not supported.", target, extension.TrimStart('.').ToUpperInvariant()), target));
            return;
        }

        if (!SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            issues.Add(new FontMergeIssue(FontMergeIssueKind.UnsupportedFormat, FontMergeIssueSeverity.Error, F("{0}格式不支持，M4 v1 仅支持 TTF/OTF 输入；合并阶段仅支持静态 TrueType/glyf 轮廓，部分 OTF/CFF 暂不支持。", "{0} format is unsupported. M4 v1 only supports TTF/OTF input; merging only supports static TrueType/glyf outlines, and some OTF/CFF fonts are not supported.", target), target));
        }
    }

    private static void ValidateDistinctInputs(FontMergeRequest request, List<FontMergeIssue> issues)
    {
        var basePath = request.BaseFont?.File.Path;
        var supplementalPath = request.SupplementalFont?.File.Path;
        if (string.IsNullOrWhiteSpace(basePath) || string.IsNullOrWhiteSpace(supplementalPath))
        {
            return;
        }

        if (PathsEqual(basePath, supplementalPath))
        {
            issues.Add(new FontMergeIssue(FontMergeIssueKind.InvalidInput, FontMergeIssueSeverity.Error, L("基础字体和补充字体不能是同一个文件。"), L("字体选择")));
        }
    }

    private static void AddLicenseIssues(FontMergeFontSelection? font, string target, List<FontMergeIssue> issues)
    {
        if (font is null || font.License.Status == LicenseStatus.Known)
        {
            return;
        }

        issues.Add(new FontMergeIssue(FontMergeIssueKind.LicenseUnknown, FontMergeIssueSeverity.Warning, F("{0}授权状态为：{1}", "{0} license status: {1}", target, font.License.Text), target));
    }

    private static void ValidateOutput(FontMergeRequest request, List<FontMergeIssue> issues)
    {
        if (!request.LicenseConfirmed)
        {
            issues.Add(new FontMergeIssue(FontMergeIssueKind.LicenseNotConfirmed, FontMergeIssueSeverity.Error, L("未完成授权确认，不能开始导出。"), L("授权确认")));
        }

        if (string.IsNullOrWhiteSpace(request.OutputFamilyName))
        {
            issues.Add(new FontMergeIssue(FontMergeIssueKind.InvalidInput, FontMergeIssueSeverity.Error, L("请输入输出字体名称。"), L("输出字体名称")));
        }

        if (string.IsNullOrWhiteSpace(request.OutputPath))
        {
            issues.Add(new FontMergeIssue(FontMergeIssueKind.OutputPathInvalid, FontMergeIssueSeverity.Error, L("请选择输出文件路径。"), L("输出文件路径")));
            return;
        }

        var extension = Path.GetExtension(request.OutputPath);
        if (!SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            issues.Add(new FontMergeIssue(FontMergeIssueKind.UnsupportedFormat, FontMergeIssueSeverity.Error, L("输出文件扩展名必须是 .ttf 或 .otf。"), L("输出文件路径")));
        }

        if (request.BaseFont is not null && PathsEqual(request.OutputPath, request.BaseFont.File.Path)
            || request.SupplementalFont is not null && PathsEqual(request.OutputPath, request.SupplementalFont.File.Path))
        {
            issues.Add(new FontMergeIssue(FontMergeIssueKind.OutputPathInvalid, FontMergeIssueSeverity.Error, L("输出不能覆盖原始字体文件。"), L("输出文件路径")));
        }

        if (File.Exists(request.OutputPath))
        {
            issues.Add(new FontMergeIssue(FontMergeIssueKind.OutputPathExists, FontMergeIssueSeverity.Error, L("输出路径已存在，M4 v1 不覆盖任何已有文件。"), L("输出文件路径")));
        }

        var directory = Path.GetDirectoryName(request.OutputPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            issues.Add(new FontMergeIssue(FontMergeIssueKind.OutputPathInvalid, FontMergeIssueSeverity.Error, L("输出目录不存在。"), L("输出文件路径")));
        }
    }

    private static FontMergeWorkerRequest CreateWorkerRequest(FontMergeRequest request, IReadOnlyList<UnicodeRange> ranges) =>
        new(
            request.BaseFont!.File.Path,
            request.SupplementalFont!.File.Path,
            ranges,
            request.OutputPath,
            request.OutputFamilyName,
            request.MergeMode);

    private static FontMergePreview CreatePreview(
        IReadOnlyList<UnicodeRange> ranges,
        IReadOnlyList<FontMergeIssue> issues,
        FontMergeWorkerPreviewResult workerResult,
        FontMergeMode mergeMode) =>
        new(
            ranges,
            issues,
            workerResult.Conflicts,
            workerResult.RequestedCodePointCount,
            workerResult.SupplementalCoverageCount,
            workerResult.MergeCodePointCount,
            workerResult.DuplicateCodePointCount,
            workerResult.MissingCodePointCount,
            workerResult.OverwrittenCodePointCount,
            BuildConflictStrategy(mergeMode));

    private static FontMergePreview EmptyPreview(IReadOnlyList<UnicodeRange> ranges, IReadOnlyList<FontMergeIssue> issues) =>
        new(ranges, issues, [], UnicodeRangeParser.CountCodePoints(ranges), 0, 0, 0, 0);

    private static FontMergeReport CreateReport(
        FontMergeRequest request,
        IReadOnlyList<UnicodeRange> ranges,
        FontMergeWorkerPreviewResult? preview,
        IReadOnlyList<FontMergeIssue> issues,
        bool succeeded,
        string errorMessage,
        string outputPath)
    {
        var rangeLabels = ranges.Select(range => range.Label).ToList();
        var skippedDuplicates = request.MergeMode == FontMergeMode.Supplement
            ? preview?.DuplicateCodePointCount ?? 0
            : 0;
        var overwrittenCodePoints = request.MergeMode == FontMergeMode.Overwrite
            ? preview?.OverwrittenCodePointCount ?? preview?.DuplicateCodePointCount ?? 0
            : 0;
        return new FontMergeReport(
            succeeded,
            outputPath,
            request.BaseFont?.FamilyName ?? "",
            request.SupplementalFont?.FamilyName ?? "",
            rangeLabels,
            preview?.RequestedCodePointCount ?? UnicodeRangeParser.CountCodePoints(ranges),
            preview?.MergeCodePointCount ?? 0,
            skippedDuplicates,
            overwrittenCodePoints,
            preview?.MissingCodePointCount ?? 0,
            preview?.DuplicateCodePointCount ?? 0,
            request.LicenseConfirmedAt,
            DateTimeOffset.UtcNow,
            errorMessage,
            issues,
            request.MergeMode);
    }

    private static string BuildConflictStrategy(FontMergeMode mergeMode) =>
        mergeMode == FontMergeMode.Overwrite
            ? L("覆盖模式：指定范围内补充字体存在的码位覆盖基础字体")
            : L("补全模式：基础字体已有码位默认跳过");

    private static async Task<string> WriteReportAsync(string outputPath, FontMergeReport report, CancellationToken cancellationToken)
    {
        var reportPath = BuildReportPath(outputPath);
        await using var stream = File.Create(reportPath);
        await JsonSerializer.SerializeAsync(stream, report, ReportJsonContext.FontMergeReport, cancellationToken).ConfigureAwait(false);
        return reportPath;
    }

    private static async Task<string> TryWriteReportAsync(string outputPath, FontMergeReport report, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                return "";
            }

            var directory = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return "";
            }

            return await WriteReportAsync(outputPath, report, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return "";
        }
    }

    private async Task LogAsync(string category, string action, string message, string? target, bool succeeded, CancellationToken cancellationToken)
    {
        if (_operationLogStore is null)
        {
            return;
        }

        await _operationLogStore.AppendOperationAsync(
            new OperationLogEntry(DateTimeOffset.UtcNow, category, action, message, target, succeeded),
            cancellationToken).ConfigureAwait(false);
    }

    private static bool PathsEqual(string first, string second)
    {
        try
        {
            return string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string ToMergeUserMessage(Exception exception)
    {
        for (var candidate = exception; candidate is not null; candidate = candidate.InnerException)
        {
            if (candidate is UnicodeRangeParseException unicodeRangeException)
            {
                return FormatUnicodeRangeParseError(unicodeRangeException);
            }
        }

        return exception.ToUserMessage();
    }

    private static string FormatUnicodeRangeParseError(UnicodeRangeParseException exception) =>
        exception.Error switch
        {
            UnicodeRangeParseError.Empty => L("请输入 Unicode 范围。"),
            UnicodeRangeParseError.InvalidRangeFormat => F("Unicode 范围格式无效：{0}", "Invalid Unicode range format: {0}", exception.Value),
            UnicodeRangeParseError.RangeStartAfterEnd => F("Unicode 范围起点不能大于终点：{0}", "Unicode range start cannot be greater than the end: {0}", exception.Value),
            UnicodeRangeParseError.InvalidCodePointFormat => F("Unicode 码位格式无效：{0}", "Invalid Unicode code point format: {0}", exception.Value),
            UnicodeRangeParseError.CodePointOutOfRange => F("Unicode 码位超出范围：{0}", "Unicode code point is out of range: {0}", exception.Value),
            UnicodeRangeParseError.CodePointInSurrogateRange => F("Unicode 码位不能位于代理区：{0}", "Unicode code point cannot be in the surrogate range: {0}", exception.Value),
            UnicodeRangeParseError.RangeIncludesSurrogateRange => F("Unicode 范围不能包含代理区：{0}", "Unicode range cannot include the surrogate range: {0}", exception.Value),
            _ => exception.Message
        };

    private sealed record RequestValidation(IReadOnlyList<UnicodeRange> Ranges, IReadOnlyList<FontMergeIssue> Issues);
}
