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

        public Task<FontMergeWorkerPreviewResult> PreviewAsync(FontMergeWorkerRequest request, IProgress<FontMergeProgress>? progress, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(CreatePreview());
        }

        public async Task<FontMergeWorkerMergeResult> MergeAsync(FontMergeWorkerRequest request, IProgress<FontMergeProgress>? progress, CancellationToken cancellationToken)
        {
            WasCalled = true;
            await File.WriteAllBytesAsync(request.OutputPath, [7, 8, 9], cancellationToken);
            return new FontMergeWorkerMergeResult(CreatePreview(), request.OutputPath);
        }

        private static FontMergeWorkerPreviewResult CreatePreview() =>
            new(
                [],
                [
                    new FontMergeConflictItem(0x0041, "A", FontMergeCodePointState.Present, FontMergeCodePointState.Present, FontMergeDecision.SkipDuplicate, "基础字体已存在"),
                    new FontMergeConflictItem(0x0042, "B", FontMergeCodePointState.Missing, FontMergeCodePointState.Present, FontMergeDecision.Merge, "将合并"),
                    new FontMergeConflictItem(0x0043, "C", FontMergeCodePointState.Missing, FontMergeCodePointState.Missing, FontMergeDecision.RecordMissing, "补充字体缺失")
                ],
                4,
                2,
                1,
                1,
                1);
    }
}
