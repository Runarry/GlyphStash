using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using GlyphStash.Application.Abstractions.Fonts;
using GlyphStash.Application.Abstractions.Storage;
using GlyphStash.Application.Fonts;
using GlyphStash.Domain.Fonts;
using GlyphStash.Localization;
using GlyphStash.Presentation.ViewModels;
using GlyphStash.Presentation.Views;

namespace GlyphStash.Presentation.Tests;

public sealed class UiScreenshotCaptureTests
{
    [Fact]
    public void CaptureAllScreensForReview()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("GLYPHSTASH_CAPTURE_UI_SCREENSHOTS"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        AppText.SetCulture(AppText.DefaultCultureCode);
        HeadlessTestHost.EnsureAvalonia();

        var outputRoot = Path.Combine(
            FindRepositoryRoot(),
            "artifacts",
            "ui-headless",
            DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(outputRoot);

        var captures = new List<string>();
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var themeDirectory = Path.Combine(outputRoot, theme.Key.ToString()!.ToLowerInvariant());
            Directory.CreateDirectory(themeDirectory);

            foreach (var scenario in RuntimeScenarios())
            {
                var targetPath = Path.Combine(themeDirectory, $"{scenario.FileName}.png");
                CaptureShellScenario(targetPath, theme, scenario.Configure);
                captures.Add(Path.GetRelativePath(outputRoot, targetPath));
            }

            var componentDemoPath = Path.Combine(themeDirectory, "18-component-demo.png");
            CaptureStandaloneView(componentDemoPath, theme, () => new ComponentDemoView { DataContext = CreateSampleViewModel() });
            captures.Add(Path.GetRelativePath(outputRoot, componentDemoPath));
        }

        var layoutDirectory = Path.Combine(outputRoot, "layout-checks");
        Directory.CreateDirectory(layoutDirectory);
        foreach (var scenario in RuntimeScenarios().Take(10))
        {
            var compactPath = Path.Combine(layoutDirectory, $"compact-800x700-{scenario.FileName}.png");
            CaptureShellScenario(compactPath, ThemeVariant.Light, scenario.Configure, 800, 700);
            captures.Add(Path.GetRelativePath(outputRoot, compactPath));

            var widePath = Path.Combine(layoutDirectory, $"wide-1600x1000-{scenario.FileName}.png");
            CaptureShellScenario(widePath, ThemeVariant.Light, scenario.Configure, 1600, 1000);
            captures.Add(Path.GetRelativePath(outputRoot, widePath));
        }

        File.WriteAllLines(
            Path.Combine(outputRoot, "manifest.txt"),
            new[] { "GlyphStash Avalonia Headless UI screenshots", "" }.Concat(captures));
    }

    private static IReadOnlyList<UiScenario> RuntimeScenarios() =>
    [
        new("01-font-library", vm => SelectPage(vm, "font-library")),
        new("02-collections", vm => SelectPage(vm, "collections")),
        new("03-online-fonts", vm => SelectPage(vm, "online-fonts")),
        new("04-merge-select-fonts", vm =>
        {
            SelectPage(vm, "merge-tool");
            vm.MergeStepIndex = 0;
        }),
        new("05-merge-ranges", vm =>
        {
            SelectPage(vm, "merge-tool");
            vm.MergeStepIndex = 1;
        }),
        new("06-merge-preview", vm =>
        {
            SelectPage(vm, "merge-tool");
            vm.MergeStepIndex = 2;
        }),
        new("07-merge-export", vm =>
        {
            SelectPage(vm, "merge-tool");
            vm.MergeStepIndex = 3;
        }),
        new("08-merge-report", vm =>
        {
            SelectPage(vm, "merge-tool");
            vm.MergeStepIndex = 4;
        }),
        new("09-glyph-browser", vm =>
        {
            SelectPage(vm, "font-library");
            vm.IsGlyphBrowserOpen = true;
        }),
        new("10-settings", vm => SelectPage(vm, "settings")),
        new("11-import-dialog", vm =>
        {
            SelectPage(vm, "font-library");
            vm.IsImportDialogOpen = true;
        }),
        new("12-tags-dialog", vm =>
        {
            SelectPage(vm, "font-library");
            vm.IsTagsDialogOpen = true;
        }),
        new("13-delete-tag-dialog", vm =>
        {
            SelectPage(vm, "font-library");
            vm.PendingDeleteTagName = "UI";
            vm.IsTagsDialogOpen = true;
            vm.IsDeleteTagDialogOpen = true;
        }),
        new("14-uninstall-dialog", vm =>
        {
            SelectPage(vm, "font-library");
            vm.IsUninstallDialogOpen = true;
        }),
        new("15-delete-collection-dialog", vm =>
        {
            SelectPage(vm, "collections");
            vm.IsDeleteCollectionDialogOpen = true;
        }),
        new("16-merge-range-dialog", vm =>
        {
            SelectPage(vm, "merge-tool");
            vm.MergeStepIndex = 1;
            vm.IsMergeRangeDialogOpen = true;
        }),
        new("17-toast", vm =>
        {
            SelectPage(vm, "font-library");
            vm.ToastMessage = "字体索引已刷新，6 个字体族可用。";
            vm.IsToastVisible = true;
        })
    ];

    private static void CaptureShellScenario(
        string path,
        ThemeVariant theme,
        Action<ShellViewModel> configure,
        double width = 1360,
        double height = 860)
    {
        var vm = CreateSampleViewModel();
        configure(vm);
        CaptureContent(path, theme, () => new ShellView { DataContext = vm }, width, height);
    }

    private static void CaptureStandaloneView(string path, ThemeVariant theme, Func<Control> createContent) =>
        CaptureContent(path, theme, createContent, 1360, 860);

    private static void CaptureContent(string path, ThemeVariant theme, Func<Control> createContent, double width, double height)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            Avalonia.Application.Current!.RequestedThemeVariant = theme;
            var window = new Window
            {
                Width = width,
                Height = height,
                RequestedThemeVariant = theme,
                Content = createContent()
            };

            window.Show();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(5);

            var bitmap = window.CaptureRenderedFrame()
                         ?? throw new InvalidOperationException("Headless renderer did not produce a frame.");
            bitmap.Save(path);
            window.Close();
        });
    }

    private static ShellViewModel CreateSampleViewModel()
    {
        var vm = ShellViewModelTestFactory.Create(ShellViewModelTestFactory.CreateLibraryService(new FakeInventory(), new FakeStore()));
        ReplaceFonts(vm, SampleFonts());

        vm.ManagedFontDirectory = @"C:\Users\sleep\GlyphStash\ManagedFonts";
        vm.GoogleFontsApiKeyText = "AIza...configured";
        vm.ScanStatus = "索引就绪：6 个字体族，缓存可用";
        vm.StatusMessage = "最近操作：字体索引已刷新";
        vm.PreviewText = "GlyphStash 字体预览 Aa 123 你好";
        vm.PreviewFontSize = 30;
        vm.SelectedFont = vm.Fonts.FirstOrDefault(font => font.FamilyName == "Noto Sans CJK SC") ?? vm.Fonts.FirstOrDefault();
        vm.SelectedPreviewFace = vm.SelectedFont?.Faces.FirstOrDefault();

        SeedCollections(vm);
        SeedTags(vm);
        SeedImportDialog(vm);
        SeedOnlineFonts(vm);
        SeedGlyphBrowser(vm);
        SeedMergeTool(vm);
        SeedSettings(vm);

        SelectPage(vm, "font-library");
        return vm;
    }

    private static IReadOnlyList<FontFamilyRecord> SampleFonts() =>
    [
        Font(
            "Noto Sans CJK SC",
            FontSourceKind.GlyphStashManaged,
            FontActivationState.TemporarilyEnabled,
            LicenseStatus.Known,
            "SIL Open Font License 1.1",
            ["CJK", "UI", "website"],
            ["官网改版", "移动端"],
            true,
            ("Regular", 400, "Normal", "OTF"),
            ("Bold", 700, "Normal", "OTF")),
        Font(
            "Inter",
            FontSourceKind.System,
            FontActivationState.Installed,
            LicenseStatus.Known,
            "SIL Open Font License 1.1",
            ["latin", "UI"],
            ["官网改版"],
            false,
            ("Regular", 400, "Normal", "TTF"),
            ("Italic", 400, "Italic", "TTF")),
        Font(
            "Source Han Serif SC",
            FontSourceKind.UserInstalled,
            FontActivationState.Installed,
            LicenseStatus.Known,
            "SIL Open Font License 1.1",
            ["CJK", "serif"],
            ["品牌手册"],
            false,
            ("Regular", 400, "Normal", "OTF"),
            ("SemiBold", 600, "Normal", "OTF")),
        Font(
            "JetBrains Mono",
            FontSourceKind.Temporary,
            FontActivationState.TemporarilyEnabled,
            LicenseStatus.Known,
            "Apache License 2.0",
            ["mono", "code"],
            ["工具界面"],
            true,
            ("Regular", 400, "Normal", "TTF")),
        Font(
            "Merriweather",
            FontSourceKind.GlyphStashManaged,
            FontActivationState.NotEnabled,
            LicenseStatus.Unknown,
            "未知授权",
            ["serif", "article"],
            ["品牌手册"],
            false,
            ("Regular", 400, "Normal", "TTF")),
        Font(
            "Material Symbols Rounded",
            FontSourceKind.GlyphStashManaged,
            FontActivationState.NotEnabled,
            LicenseStatus.ExternalLink,
            "请查看来源页面：https://fonts.google.com/icons",
            ["icons"],
            ["工具界面"],
            false,
            ("Regular", 400, "Normal", "WOFF2"))
    ];

    private static FontFamilyRecord Font(
        string familyName,
        FontSourceKind sourceKind,
        FontActivationState activationState,
        LicenseStatus licenseStatus,
        string licenseText,
        IReadOnlyList<string> tags,
        IReadOnlyList<string> collections,
        bool isFavorite,
        params (string Subfamily, int Weight, string Slant, string Format)[] faces)
    {
        var lastSeen = DateTimeOffset.Now.AddDays(-2);
        return new FontFamilyRecord(
            familyName,
            faces.Select(face =>
            {
                var normalizedFamily = familyName.Replace(' ', '-');
                var path = $@"C:\Users\sleep\GlyphStash\ManagedFonts\{normalizedFamily}-{face.Subfamily}.{face.Format.ToLowerInvariant()}";
                var file = new FontFileRecord(path, face.Format, $"sha256-{normalizedFamily}-{face.Subfamily}", sourceKind, lastSeen);
                return new FontFaceRecord(
                    familyName,
                    face.Subfamily,
                    $"{familyName} {face.Subfamily}",
                    $"{normalizedFamily}-{face.Subfamily}",
                    face.Weight,
                    "Normal",
                    face.Slant,
                    file);
            }).ToList(),
            sourceKind,
            activationState,
            licenseStatus,
            licenseText,
            tags,
            collections,
            isFavorite);
    }

    private static void SeedCollections(ShellViewModel vm)
    {
        var collections = new[]
        {
            new FontCollectionRecord("官网改版", ["Noto Sans CJK SC", "Inter"], 1, 0, DateTimeOffset.Now.AddHours(-6)),
            new FontCollectionRecord("品牌手册", ["Source Han Serif SC", "Merriweather"], 0, 1, DateTimeOffset.Now.AddDays(-3)),
            new FontCollectionRecord("工具界面", ["JetBrains Mono", "Material Symbols Rounded"], 1, 1, null)
        };

        foreach (var collection in collections)
        {
            vm.Collections.Add(new CollectionItemViewModel(collection));
            vm.AvailableCollections.Add(collection.Name);
            vm.CollectionFilters.Add(collection.Name);
            vm.CollectionFilterOptions.Add(new LocalizedOptionViewModel(collection.Name, collection.Name));
        }

        vm.SelectedCollection = vm.Collections.First();
        foreach (var font in vm.Fonts.Where(font => vm.SelectedCollection.FamilyNames.Contains(font.FamilyName)))
        {
            vm.CollectionFonts.Add(font);
        }
    }

    private static void SeedTags(ShellViewModel vm)
    {
        foreach (var tag in new[] { new TagRecord("UI", 3), new TagRecord("CJK", 2), new TagRecord("mono", 1), new TagRecord("serif", 2) })
        {
            vm.AvailableTags.Add(tag);
            vm.TagFilters.Add(tag.Name);
            vm.TagFilterOptions.Add(new LocalizedOptionViewModel(tag.Name, tag.Name));
            vm.TagOptions.Add(new NameOptionViewModel(tag.Name, tag.FontCount, tag.Name is "UI" or "CJK"));
        }

        foreach (var collection in vm.Collections)
        {
            vm.CollectionOptions.Add(new NameOptionViewModel(collection.Name, collection.FontCount, collection.Name == "官网改版"));
        }

        vm.TagEditorText = "display, print";
        vm.CollectionEditorText = "活动主视觉";
    }

    private static void SeedImportDialog(ShellViewModel vm)
    {
        vm.ImportStatus = "已解析 3 个文件；2 个可导入，1 个需要处理。";
        vm.ImportTagsText = "UI, CJK";
        vm.ImportCollectionsText = "官网改版";
        vm.ImportPreviewItems.Add(new ImportPreviewItemViewModel(new FontImportPreviewItem(
            @"D:\Downloads\NotoSansSC-Regular.otf",
            "NotoSansSC-Regular.otf",
            "OTF",
            "Noto Sans SC",
            "Regular",
            "Noto Sans SC Regular",
            "NotoSansSC-Regular",
            "2.004",
            "Google",
            "SIL Open Font License 1.1",
            "sha256-noto-regular",
            true,
            true,
            true,
            "可导入")));
        vm.ImportPreviewItems.Add(new ImportPreviewItemViewModel(new FontImportPreviewItem(
            @"D:\Downloads\PosterDisplay.ttf",
            "PosterDisplay.ttf",
            "TTF",
            "Poster Display",
            "Bold",
            "Poster Display Bold",
            "PosterDisplay-Bold",
            "1.0",
            "Example Foundry",
            "未知授权",
            "sha256-poster-display",
            true,
            true,
            true,
            "可导入")));
        vm.ImportPreviewItems.Add(new ImportPreviewItemViewModel(new FontImportPreviewItem(
            @"D:\Downloads\LegacyBitmap.fon",
            "LegacyBitmap.fon",
            "FON",
            "",
            "",
            "",
            "",
            null,
            null,
            null,
            null,
            false,
            false,
            false,
            "不可导入",
            "当前字体库不支持该格式导入，仅支持 TTF、OTF、TTC、OTC。")));
    }

    private static void SeedOnlineFonts(ShellViewModel vm)
    {
        var noto = new RemoteFontFamily(
            "google-fonts",
            "Noto Sans",
            "sans-serif",
            ["latin", "latin-ext", "cyrillic", "greek", "vietnamese"],
            "v34",
            new DateOnly(2026, 3, 18),
            "https://fonts.google.com/specimen/Noto+Sans",
            "请查看来源页面：https://fonts.google.com/specimen/Noto+Sans",
            [
                new RemoteFontStyle("regular", "noto-sans-v34-latin-regular.ttf", "https://fonts.gstatic.com/s/notosans/regular.ttf"),
                new RemoteFontStyle("700", "noto-sans-v34-latin-700.ttf", "https://fonts.gstatic.com/s/notosans/700.ttf"),
                new RemoteFontStyle("italic", "noto-sans-v34-latin-italic.ttf", "https://fonts.gstatic.com/s/notosans/italic.ttf")
            ]);
        var roboto = new RemoteFontFamily(
            "google-fonts",
            "Roboto Flex",
            "sans-serif",
            ["latin", "latin-ext"],
            "v26",
            new DateOnly(2026, 2, 4),
            "https://fonts.google.com/specimen/Roboto+Flex",
            "请查看来源页面：https://fonts.google.com/specimen/Roboto+Flex",
            [new RemoteFontStyle("regular", "roboto-flex.ttf", "https://fonts.gstatic.com/s/robotoflex/regular.ttf")]);

        vm.RemoteFonts.Add(new RemoteFontFamilyItemViewModel(noto));
        vm.RemoteFonts.Add(new RemoteFontFamilyItemViewModel(roboto));
        vm.SelectedRemoteFont = vm.RemoteFonts.First();
        vm.OnlineStatus = "找到 2 个匹配字体；下载前请确认来源页 license。";
        vm.DownloadTagsText = "online, UI";
        vm.DownloadCollectionsText = "官网改版";
        vm.DownloadFavorite = true;

        var completed = new OnlineFontDownloadQueueItemViewModel(
            noto,
            noto.Styles.Take(2).ToList(),
            new OnlineFontImportOptions(["online", "UI"], ["官网改版"], true, false, true));
        completed.MarkSucceeded("已下载 2 个样式并写入管理目录。");
        vm.OnlineDownloadQueue.Add(completed);

        var failed = new OnlineFontDownloadQueueItemViewModel(
            roboto,
            roboto.Styles,
            new OnlineFontImportOptions([], ["工具界面"], false, false, false));
        failed.MarkFailed("Google Fonts API key 无效或没有权限。");
        vm.OnlineDownloadQueue.Add(failed);
    }

    private static void SeedGlyphBrowser(ShellViewModel vm)
    {
        var glyphs = new[]
        {
            ('A', 0x0041, "A"), ('B', 0x0042, "B"), ('C', 0x0043, "C"), ('你', 0x4F60, "uni4F60"),
            ('好', 0x597D, "uni597D"), ('字', 0x5B57, "uni5B57"), ('形', 0x5F62, "uni5F62"), ('。', 0x3002, "uni3002"),
            ('，', 0xFF0C, "uniFF0C"), ('一', 0x4E00, "uni4E00"), ('二', 0x4E8C, "uni4E8C"), ('三', 0x4E09, "uni4E09")
        };

        var glyphId = 100;
        foreach (var glyph in glyphs)
        {
            vm.Glyphs.Add(new GlyphItemViewModel(new GlyphRecord(glyph.Item1.ToString(), glyph.Item2, glyphId++, glyph.Item3, "Regular", true)));
        }

        vm.UnicodeBlocks.Add("Basic Latin");
        vm.UnicodeBlocks.Add("CJK Unified Ideographs");
        vm.UnicodeBlocks.Add("CJK Symbols and Punctuation");
        vm.SelectedUnicodeBlock = "全部区块";
        vm.GlyphTotalPages = 4;
        vm.SelectedGlyph = vm.Glyphs.First(glyph => glyph.Character == "你");
        vm.GlyphStatus = "当前显示 12 / 436 个 Unicode 映射字形。";
        SetPrivateField(vm, "_currentGlyphFace", vm.SelectedPreviewFace);
    }

    private static void SeedMergeTool(ShellViewModel vm)
    {
        vm.SelectedMergeBaseFont = vm.Fonts.FirstOrDefault(font => font.FamilyName == "Noto Sans CJK SC");
        vm.SelectedMergeSupplementalFont = vm.Fonts.FirstOrDefault(font => font.FamilyName == "Source Han Serif SC");
        vm.MergeBaseInput.SetMetadata(new FontMetadata(
            @"C:\Fonts\NotoSansCJKSC-Regular.ttf",
            "TTF",
            "Noto Sans CJK SC",
            "Regular",
            "Noto Sans CJK SC Regular",
            "NotoSansCJKSC-Regular",
            null,
            null,
            "OFL",
            "hash-base"));
        vm.MergeSupplementalInput.SetMetadata(new FontMetadata(
            @"C:\Fonts\SourceHanSerifSC-Regular.ttf",
            "TTF",
            "Source Han Serif SC",
            "Regular",
            "Source Han Serif SC Regular",
            "SourceHanSerifSC-Regular",
            null,
            null,
            "未知授权",
            "hash-patch"));
        vm.MergeUnicodeRanges = "U+4E00-U+9FFF, U+3000-U+303F";
        vm.MergeOutputFontName = "Noto Sans CJK SC Patch";
        vm.MergeOutputPath = @"C:\Users\sleep\GlyphStash\Exports\NotoSansCJKSC-Patch.ttf";
        vm.MergeLicenseConfirmed = true;
        vm.MergeStatus = "冲突预览完成，可继续授权与导出。";

        var preview = new FontMergePreview(
            [new UnicodeRange(0x4E00, 0x9FFF), new UnicodeRange(0x3000, 0x303F)],
            [
                new FontMergeIssue(FontMergeIssueKind.LicenseUnknown, FontMergeIssueSeverity.Warning, "补充字体 license 需要人工确认。", "Source Han Serif SC"),
                new FontMergeIssue(FontMergeIssueKind.OpenTypeLayoutConflict, FontMergeIssueSeverity.Info, "OpenType layout 差异已记录到报告。", "GSUB/GPOS")
            ],
            [
                new FontMergeConflictItem(0x4F60, "你", FontMergeCodePointState.Present, FontMergeCodePointState.Present, FontMergeDecision.SkipDuplicate, "基础字体已存在"),
                new FontMergeConflictItem(0x9FA5, "龥", FontMergeCodePointState.Missing, FontMergeCodePointState.Present, FontMergeDecision.Merge, "补充字体可提供"),
                new FontMergeConflictItem(0x3002, "。", FontMergeCodePointState.Present, FontMergeCodePointState.Present, FontMergeDecision.SkipDuplicate, "符号重复"),
                new FontMergeConflictItem(0x2B740, "𫝀", FontMergeCodePointState.Missing, FontMergeCodePointState.Missing, FontMergeDecision.RecordMissing, "两侧均缺失")
            ],
            21056,
            18320,
            4200,
            13884,
            2972,
            0,
            "补全模式：基础字体已有码位默认跳过");
        InvokePrivate(vm, "ApplyMergePreview", preview);

        var baseCoverage = new GlyphCoverage(
            [new UnicodeRange(0x3000, 0x303F), new UnicodeRange(0x4E00, 0x7FFF)],
            [],
            [],
            12416);
        var supplementalCoverage = new GlyphCoverage(
            [new UnicodeRange(0x3000, 0x303F), new UnicodeRange(0x4E00, 0x9FFF)],
            [],
            [],
            21056);
        InvokePrivate(vm, "BuildMergeRangeComparison", baseCoverage, supplementalCoverage);
        foreach (var segment in vm.MergeRangeSegments.Take(3))
        {
            segment.IsSelected = true;
        }

        vm.MergeRangeBaseSummary = "Noto Sans CJK SC · Regular · 12,416 个 Unicode 映射码位 · 2 个连续段";
        vm.MergeRangeSupplementalSummary = "Source Han Serif SC · Regular · 21,056 个 Unicode 映射码位 · 2 个连续段";
        vm.MergeRangeDialogStatus = "读取完成：可选择补充字体覆盖而基础字体缺失的范围。";

        var report = new FontMergeReport(
            true,
            vm.MergeOutputPath,
            "Noto Sans CJK SC",
            "Source Han Serif SC",
            ["U+4E00-U+9FFF", "U+3000-U+303F"],
            21056,
            4200,
            13884,
            0,
            2972,
            4,
            DateTimeOffset.Now.AddMinutes(-12),
            DateTimeOffset.Now.AddMinutes(-9),
            "",
            preview.Issues,
            FontMergeMode.Supplement);
        InvokePrivate(vm, "ApplyMergeReport", report, @"C:\Users\sleep\GlyphStash\Exports\NotoSansCJKSC-Patch.report.json");
    }

    private static void SeedSettings(ShellViewModel vm)
    {
        foreach (var language in AppText.SupportedLanguages)
        {
            vm.LanguageOptions.Add(new LanguageOptionViewModel(language));
        }

        vm.SelectedLanguage = vm.LanguageOptions.First();
        vm.OperationLogs.Add(new OperationLogItemViewModel(new OperationLogEntry(DateTimeOffset.Now.AddMinutes(-4), "字体索引", "rescan", "字体索引已刷新", null, true)));
        vm.OperationLogs.Add(new OperationLogItemViewModel(new OperationLogEntry(DateTimeOffset.Now.AddMinutes(-14), "Google Fonts 下载", "download", "Noto Sans 已下载 2 个样式", "Noto Sans", true)));
        vm.OperationLogs.Add(new OperationLogItemViewModel(new OperationLogEntry(DateTimeOffset.Now.AddMinutes(-31), "合并工具", "preview", "冲突预览完成，可继续授权与导出。", "Noto Sans CJK SC Patch", true)));
        vm.OperationLogs.Add(new OperationLogItemViewModel(new OperationLogEntry(DateTimeOffset.Now.AddHours(-2), "Google Fonts 请求", "search", "Google Fonts API key 无效或没有权限。", "Roboto Flex", false)));
    }

    private static void SelectPage(ShellViewModel vm, string key)
    {
        vm.SelectedNavigationItem = vm.NavigationItems.Single(item => item.Key == key);
        vm.IsGlyphBrowserOpen = false;
    }

    private static void ReplaceFonts(ShellViewModel vm, IReadOnlyList<FontFamilyRecord> records) =>
        InvokePrivate(vm, "ReplaceFonts", records);

    private static void InvokePrivate(ShellViewModel vm, string methodName, params object?[] args)
    {
        var method = typeof(ShellViewModel).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                     ?? throw new MissingMethodException(typeof(ShellViewModel).FullName, methodName);
        method.Invoke(vm, args);
    }

    private static void SetPrivateField(ShellViewModel vm, string fieldName, object? value)
    {
        var field = typeof(ShellViewModel).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new MissingFieldException(typeof(ShellViewModel).FullName, fieldName);
        field.SetValue(vm, value);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GlyphStash.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate GlyphStash repository root.");
    }

    private sealed record UiScenario(string FileName, Action<ShellViewModel> Configure);

    private sealed class FakeInventory : IFontInventoryService
    {
        public Task<IReadOnlyList<FontFamilyRecord>> ScanInstalledFontsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FontFamilyRecord>>([]);
    }

    private sealed class FakeStore : IFontMetadataStore
    {
        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SaveFontIndexAsync(IReadOnlyList<FontFamilyRecord> fonts, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<FontFamilyRecord>> SearchAsync(FontSearchQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FontFamilyRecord>>([]);
    }
}
