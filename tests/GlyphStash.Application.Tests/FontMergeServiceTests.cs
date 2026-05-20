using GlyphStash.Application.Abstractions.Fonts;
using GlyphStash.Application.Fonts;
using GlyphStash.Domain.Fonts;

namespace GlyphStash.Application.Tests;

public sealed class FontMergeServiceTests
{
    [Fact]
    public async Task Preview_RejectsUnsupportedCollectionFontsBeforeWorker()
    {
        var directory = CreateTempDirectory();
        var basePath = Path.Combine(directory, "Base.ttf");
        var supplementalPath = Path.Combine(directory, "Patch.ttc");
        await File.WriteAllBytesAsync(basePath, [1], CancellationToken.None);
        await File.WriteAllBytesAsync(supplementalPath, [1], CancellationToken.None);
        var worker = new FakeMergeWorker();
        var service = new FontMergeService(worker);

        var preview = await service.PreviewAsync(CreateRequest(basePath, supplementalPath), CancellationToken.None);

        Assert.True(preview.HasBlockingIssues);
        Assert.Contains(preview.Issues, issue => issue.Kind == FontMergeIssueKind.UnsupportedFontKind);
        Assert.False(worker.WasCalled);
    }

    [Fact]
    public async Task Merge_RequiresLicenseConfirmation()
    {
        var directory = CreateTempDirectory();
        var basePath = Path.Combine(directory, "Base.ttf");
        var supplementalPath = Path.Combine(directory, "Patch.ttf");
        await File.WriteAllBytesAsync(basePath, [1], CancellationToken.None);
        await File.WriteAllBytesAsync(supplementalPath, [1], CancellationToken.None);
        var service = new FontMergeService(new FakeMergeWorker());
        var request = CreateRequest(basePath, supplementalPath) with
        {
            OutputPath = Path.Combine(directory, "Merged.ttf"),
            LicenseConfirmed = false
        };

        var result = await service.MergeAsync(request, null, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Issues, issue => issue.Kind == FontMergeIssueKind.LicenseNotConfirmed);
    }

    [Fact]
    public async Task Merge_BlocksExistingOutputPath()
    {
        var directory = CreateTempDirectory();
        var basePath = Path.Combine(directory, "Base.ttf");
        var supplementalPath = Path.Combine(directory, "Patch.ttf");
        var outputPath = Path.Combine(directory, "Merged.ttf");
        await File.WriteAllBytesAsync(basePath, [1], CancellationToken.None);
        await File.WriteAllBytesAsync(supplementalPath, [1], CancellationToken.None);
        await File.WriteAllBytesAsync(outputPath, [1], CancellationToken.None);
        var service = new FontMergeService(new FakeMergeWorker());

        var result = await service.MergeAsync(CreateRequest(basePath, supplementalPath) with { OutputPath = outputPath }, null, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Issues, issue => issue.Kind == FontMergeIssueKind.OutputPathExists);
    }

    [Fact]
    public async Task Merge_WritesOutputAndJsonReport()
    {
        var directory = CreateTempDirectory();
        var basePath = Path.Combine(directory, "Base.ttf");
        var supplementalPath = Path.Combine(directory, "Patch.ttf");
        var outputPath = Path.Combine(directory, "Merged.ttf");
        await File.WriteAllBytesAsync(basePath, [1], CancellationToken.None);
        await File.WriteAllBytesAsync(supplementalPath, [1], CancellationToken.None);
        var service = new FontMergeService(new FakeMergeWorker());

        var result = await service.MergeAsync(CreateRequest(basePath, supplementalPath) with { OutputPath = outputPath }, null, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(File.Exists(outputPath));
        Assert.True(File.Exists(result.ReportPath));
        Assert.Equal("Merged.ttf.glyphstash-merge-report.json", Path.GetFileName(result.ReportPath));

        var report = await service.ReadReportAsync(result.ReportPath, CancellationToken.None);
        Assert.True(report.Succeeded);
        Assert.Equal(1, report.MergedCodePointCount);
        Assert.Equal(1, report.SkippedDuplicateCodePointCount);
        Assert.Equal(0, report.OverwrittenCodePointCount);
        Assert.Equal(FontMergeMode.Supplement, report.MergeMode);
    }

    [Fact]
    public async Task Merge_ForwardsOverwriteModeAndReportsOverwriteCount()
    {
        var directory = CreateTempDirectory();
        var basePath = Path.Combine(directory, "Base.ttf");
        var supplementalPath = Path.Combine(directory, "Patch.ttf");
        var outputPath = Path.Combine(directory, "Merged.ttf");
        await File.WriteAllBytesAsync(basePath, [1], CancellationToken.None);
        await File.WriteAllBytesAsync(supplementalPath, [1], CancellationToken.None);
        var worker = new FakeMergeWorker();
        var service = new FontMergeService(worker);

        var result = await service.MergeAsync(
            CreateRequest(basePath, supplementalPath) with
            {
                OutputPath = outputPath,
                MergeMode = FontMergeMode.Overwrite
            },
            null,
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(FontMergeMode.Overwrite, worker.LastRequest?.MergeMode);
        Assert.Equal(FontMergeMode.Overwrite, result.Report.MergeMode);
        Assert.Equal(0, result.Report.SkippedDuplicateCodePointCount);
        Assert.Equal(1, result.Report.OverwrittenCodePointCount);
        Assert.Contains("覆盖 1", result.Summary, StringComparison.Ordinal);
    }

    private static FontMergeRequest CreateRequest(string basePath, string supplementalPath) =>
        new(
            CreateSelection("Base Font", basePath),
            CreateSelection("Patch Font", supplementalPath),
            "U+0041-U+0043, U+4E00",
            "",
            "Merged Font",
            true,
            DateTimeOffset.UtcNow);

    private static FontMergeFontSelection CreateSelection(string familyName, string path) =>
        new(
            familyName,
            "Regular 400",
            new FontFileRef(path, Path.GetExtension(path).TrimStart('.').ToUpperInvariant()),
            new LicenseSnapshot(LicenseStatus.Known, "OFL"));

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "GlyphStash.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class FakeMergeWorker : IFontMergeWorker
    {
        public bool WasCalled { get; private set; }

        public FontMergeWorkerRequest? LastRequest { get; private set; }

        public Task<FontMergeWorkerPreviewResult> PreviewAsync(FontMergeWorkerRequest request, IProgress<FontMergeProgress>? progress, CancellationToken cancellationToken)
        {
            WasCalled = true;
            LastRequest = request;
            return Task.FromResult(CreatePreview(request.MergeMode));
        }

        public async Task<FontMergeWorkerMergeResult> MergeAsync(FontMergeWorkerRequest request, IProgress<FontMergeProgress>? progress, CancellationToken cancellationToken)
        {
            WasCalled = true;
            LastRequest = request;
            await File.WriteAllBytesAsync(request.OutputPath, [7, 8, 9], cancellationToken);
            return new FontMergeWorkerMergeResult(CreatePreview(request.MergeMode), request.OutputPath);
        }

        private static FontMergeWorkerPreviewResult CreatePreview(FontMergeMode mergeMode)
        {
            var duplicateDecision = mergeMode == FontMergeMode.Overwrite
                ? FontMergeDecision.Overwrite
                : FontMergeDecision.SkipDuplicate;
            var duplicateNote = mergeMode == FontMergeMode.Overwrite
                ? "覆盖基础字体"
                : "基础字体已存在";

            return new FontMergeWorkerPreviewResult(
                [],
                [
                    new FontMergeConflictItem(0x0041, "A", FontMergeCodePointState.Present, FontMergeCodePointState.Present, duplicateDecision, duplicateNote),
                    new FontMergeConflictItem(0x0042, "B", FontMergeCodePointState.Missing, FontMergeCodePointState.Present, FontMergeDecision.Merge, "将合并"),
                    new FontMergeConflictItem(0x0043, "C", FontMergeCodePointState.Missing, FontMergeCodePointState.Missing, FontMergeDecision.RecordMissing, "补充字体缺失")
                ],
                4,
                2,
                1,
                1,
                1,
                mergeMode == FontMergeMode.Overwrite ? 1 : 0);
        }
    }
}
