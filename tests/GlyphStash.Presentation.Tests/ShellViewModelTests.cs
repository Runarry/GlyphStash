using System.Collections.Specialized;
using Avalonia.Media;
using GlyphStash.Application.Abstractions.App;
using GlyphStash.Application.Abstractions.Fonts;
using GlyphStash.Application.Abstractions.Storage;
using GlyphStash.Application.Fonts;
using GlyphStash.Domain.Fonts;
using GlyphStash.Localization;
using GlyphStash.Presentation.Services;
using GlyphStash.Presentation.ViewModels;
using DomainUnicodeRange = GlyphStash.Domain.Fonts.UnicodeRange;

namespace GlyphStash.Presentation.Tests;

public sealed class ShellViewModelTests
{
    [Fact]
    public async Task Initialize_LoadsCachedFontsAndFiltersBySearch()
    {
        var store = new FakeStore([CreateFont("Inter"), CreateFont("Cascadia Code")]);
        var vm = ShellViewModelTestFactory.Create(ShellViewModelTestFactory.CreateLibraryService(new FakeInventory([]), store));

        await vm.InitializeAsync();
        vm.SearchText = "Cascadia";

        if (vm.Fonts.Count != 1 || vm.SelectedFont?.FamilyName != "Cascadia Code")
        {
            throw new InvalidOperationException("Expected search to keep only Cascadia Code selected.");
        }
    }

    [Fact]
    public async Task PreviewSettings_UpdateFontItems()
    {
        var store = new FakeStore([CreateFont("Inter")]);
        var vm = ShellViewModelTestFactory.Create(ShellViewModelTestFactory.CreateLibraryService(new FakeInventory([]), store));

        await vm.InitializeAsync();
        vm.PreviewText = "Hello 字体";
        vm.PreviewFontSize = 42;

        if (vm.Fonts[0].PreviewText != "Hello 字体" || Math.Abs(vm.Fonts[0].PreviewFontSize - 42) > 0.01)
        {
            throw new InvalidOperationException("Expected preview settings to propagate to visible font items.");
        }
    }

    [Fact]
    public async Task Initialize_DefaultsToFontLibraryNavigation()
    {
        var vm = ShellViewModelTestFactory.Create(ShellViewModelTestFactory.CreateLibraryService(new FakeInventory([]), new FakeStore([CreateFont("Inter")])));

        await vm.InitializeAsync();

        if (!vm.IsFontLibraryPage || vm.SelectedNavigationItem?.Key != "font-library")
        {
            throw new InvalidOperationException("Expected font library to be the default page.");
        }
    }

    [Fact]
    public async Task LanguageSelection_PersistsAndUpdatesLocalizationService()
    {
        AppText.SetCulture("zh-CN");
        var settingsStore = new FakeSettingsStore();
        var service = CreateLocalService(new FakePlatformActivation(), [], settingsStore: settingsStore);
        var localization = new AppLocalizationService();
        var vm = ShellViewModelTestFactory.Create(
            ShellViewModelTestFactory.CreateLibraryService(new FakeInventory([]), new FakeStore([CreateFont("Inter")])),
            service,
            NullUserFileDialogService.Instance,
            localizationService: localization);

        await vm.InitializeAsync();
        vm.SelectedLanguage = vm.LanguageOptions.Single(language => language.CultureCode == "en-US");
        await Task.Delay(50);

        Assert.Equal("en-US", localization.CurrentCulture.Name);
        Assert.Equal("en-US", (await settingsStore.GetSettingsAsync(CancellationToken.None))?.UiCultureCode);
        Assert.Equal("Configured", AppText.Get("Common.Configured"));

        AppText.SetCulture("zh-CN");
    }

    [Fact]
    public async Task CheckForAppUpdates_ShowsAvailableUpdateAndEnablesDownload()
    {
        var updateService = new FakeAppUpdateService(
            AppUpdateCheckResult.Available("0.1.0", "0.2.0", "发现新版本，可以下载并重启更新。", "Release notes"));
        var vm = ShellViewModelTestFactory.Create(
            ShellViewModelTestFactory.CreateLibraryService(new FakeInventory([]), new FakeStore([CreateFont("Inter")])),
            appUpdateService: updateService);

        await vm.InitializeAsync();
        await vm.CheckForAppUpdatesCommand.ExecuteAsync(null);

        Assert.True(vm.CanDownloadAppUpdate);
        Assert.Equal("0.2.0", vm.AppUpdateAvailableVersion);
        Assert.Equal("Release notes", vm.AppUpdateReleaseNotes);
        Assert.Contains("新版本", vm.AppUpdateStatus, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DownloadAndRestartAppUpdate_ReportsProgressAndFailure()
    {
        var updateService = new FakeAppUpdateService(
            AppUpdateCheckResult.Available("0.1.0", "0.2.0", "available", ""));
        updateService.ApplyResult = new AppUpdateApplyResult(false, "failed");
        var vm = ShellViewModelTestFactory.Create(
            ShellViewModelTestFactory.CreateLibraryService(new FakeInventory([]), new FakeStore([CreateFont("Inter")])),
            appUpdateService: updateService);

        await vm.InitializeAsync();
        await vm.CheckForAppUpdatesCommand.ExecuteAsync(null);
        await vm.DownloadAndRestartAppUpdateCommand.ExecuteAsync(null);

        Assert.Equal(42, vm.AppUpdateProgress);
        Assert.Equal("failed", vm.AppUpdateStatus);
    }

    [Fact]
    public async Task SettingStringFilter_ResolvesSelectedOptionInstance()
    {
        var vm = ShellViewModelTestFactory.Create(
            ShellViewModelTestFactory.CreateLibraryService(new FakeInventory([]), new FakeStore([CreateFont("Inter")])));

        await vm.InitializeAsync();
        vm.SelectedTagFilter = "全部标签";
        vm.SelectedOnlineSubset = "latin-ext";
        vm.SelectedMergeModeLabel = "覆盖";

        Assert.Equal("全部标签", vm.SelectedTagFilterOption?.Value);
        Assert.Equal("latin-ext", vm.SelectedOnlineSubsetOption?.Value);
        Assert.Equal("覆盖", vm.SelectedMergeModeOptionByValue?.Value);
    }

    [Fact]
    public async Task SettingOptionSelection_UpdatesExistingStringApi()
    {
        var vm = ShellViewModelTestFactory.Create(
            ShellViewModelTestFactory.CreateLibraryService(new FakeInventory([]), new FakeStore([CreateFont("Inter")])));

        await vm.InitializeAsync();
        vm.SelectedOnlineCategoryOption = vm.OnlineCategoryOptionModels.Single(option => option.Value == "sans-serif");
        vm.SelectedMergeModeOptionByValue = vm.MergeModeOptionModels.Single(option => option.Value == "覆盖");

        Assert.Equal("sans-serif", vm.SelectedOnlineCategory);
        Assert.Equal("覆盖", vm.SelectedMergeModeLabel);
    }

    [Fact]
    public async Task Constructor_DoesNotOverwritePersistedLanguageBeforeSettingsLoad()
    {
        AppText.SetCulture("zh-CN");
        try
        {
            var settingsStore = new FakeSettingsStore(uiCultureCode: "en-US");
            var service = CreateLocalService(new FakePlatformActivation(), [], settingsStore: settingsStore);
            var localization = new AppLocalizationService();
            var vm = ShellViewModelTestFactory.Create(
                ShellViewModelTestFactory.CreateLibraryService(new FakeInventory([]), new FakeStore([CreateFont("Inter")])),
                service,
                NullUserFileDialogService.Instance,
                localizationService: localization);

            await Task.Delay(50);
            Assert.Equal(0, settingsStore.SaveCount);
            Assert.Equal("en-US", (await settingsStore.GetSettingsAsync(CancellationToken.None))?.UiCultureCode);

            await vm.InitializeAsync();

            Assert.Equal("en-US", localization.CurrentCulture.Name);
            Assert.Equal(0, settingsStore.SaveCount);
            Assert.Equal("en-US", (await settingsStore.GetSettingsAsync(CancellationToken.None))?.UiCultureCode);
        }
        finally
        {
            AppText.SetCulture("zh-CN");
        }
    }

    [Fact]
    public void CultureChange_LocalizesStableViewModelLabels()
    {
        AppText.SetCulture("zh-CN");
        try
        {
            var localization = new AppLocalizationService();
            var vm = ShellViewModelTestFactory.Create(
                ShellViewModelTestFactory.CreateLibraryService(new FakeInventory([]), new FakeStore([])),
                null,
                NullUserFileDialogService.Instance,
                localizationService: localization);

            localization.SetCulture("en-US");

            Assert.Equal("1 Select fonts", vm.MergeSteps[0].Label);
            Assert.Equal("All subsets", vm.OnlineSubsetOptionModels[0].DisplayName);
            Assert.Equal("All categories", vm.OnlineCategoryOptionModels[0].DisplayName);
            Assert.Equal("Select a base font and supplemental font.", vm.MergeStatus);
            Assert.Contains("Notepad: covers newly started", vm.CompatibilityMatrix[0], StringComparison.Ordinal);
            Assert.Equal("Unknown license", new FontFamilyItemViewModel(CreateFont("Inter")).LicenseLabel);

            var linkedFont = new FontFamilyItemViewModel(CreateFont("Noto Sans") with
            {
                LicenseStatus = LicenseStatus.ExternalLink,
                LicenseText = "请查看来源页面：https://fonts.example/Noto"
            });
            Assert.Equal("See source page: https://fonts.example/Noto", linkedFont.LicenseLabel);
        }
        finally
        {
            AppText.SetCulture("zh-CN");
        }
    }

    [Fact]
    public async Task Navigation_HidesComponentDemoAndCanSwitchToImplementedPages()
    {
        var vm = ShellViewModelTestFactory.Create(ShellViewModelTestFactory.CreateLibraryService(new FakeInventory([]), new FakeStore([CreateFont("Inter")])));
        await vm.InitializeAsync();

        if (vm.NavigationItems.Any(item => item.Key == "component-demo"))
        {
            throw new InvalidOperationException("Expected component demo page to be hidden from main navigation.");
        }

        vm.SelectedNavigationItem = vm.NavigationItems.Single(item => item.Key == "collections");
        if (!vm.IsCollectionsPage)
        {
            throw new InvalidOperationException("Expected collections page to be implemented in M2.");
        }

        vm.SelectedNavigationItem = vm.NavigationItems.Single(item => item.Key == "online-fonts");
        if (!vm.IsOnlineFontsPage || vm.CurrentPageMilestone != "M3")
        {
            throw new InvalidOperationException("Expected online fonts page to be implemented in M3.");
        }

        vm.SelectedNavigationItem = vm.NavigationItems.Single(item => item.Key == "merge-tool");
        if (!vm.IsMergeToolPage || vm.CurrentPageMilestone != "M4")
        {
            throw new InvalidOperationException("Expected merge tool page to be implemented in M4.");
        }
    }

    [Fact]
    public async Task MergeWizard_PreviewsConflictsAndRequiresLicenseConfirmation()
    {
        var directory = Path.Combine(Path.GetTempPath(), "GlyphStash.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var basePath = Path.Combine(directory, "Base.ttf");
        var supplementalPath = Path.Combine(directory, "Patch.ttf");
        var outputPath = Path.Combine(directory, "Merged.ttf");
        await File.WriteAllBytesAsync(basePath, [1], CancellationToken.None);
        await File.WriteAllBytesAsync(supplementalPath, [1], CancellationToken.None);
        var fonts =
            new[]
            {
                CreateFontWithFaces("Base", ("Regular", 400, "Normal", basePath)),
                CreateFontWithFaces("Patch", ("Regular", 400, "Normal", supplementalPath))
            };
        var vm = ShellViewModelTestFactory.Create(
            ShellViewModelTestFactory.CreateLibraryService(new FakeInventory([]), new FakeStore(fonts)),
            null,
            NullUserFileDialogService.Instance,
            fontMergeService: new FontMergeService(new FakeMergeWorker()));

        await vm.InitializeAsync();
        vm.SelectedNavigationItem = vm.NavigationItems.Single(item => item.Key == "merge-tool");
        vm.MergeUnicodeRanges = "U+0041";
        vm.SelectedMergeModeLabel = "覆盖";
        await vm.NextMergeStepCommand.ExecuteAsync(null);
        await vm.NextMergeStepCommand.ExecuteAsync(null);

        Assert.True(vm.IsMergeStepPreview);
        Assert.True(vm.HasMergeConflicts);
        Assert.Contains("覆盖", vm.MergeDefaultStrategy, StringComparison.Ordinal);
        Assert.Contains("覆盖", vm.MergeConflicts[0].DecisionLabel, StringComparison.Ordinal);

        vm.MergeOutputPath = outputPath;
        vm.MergeStepIndex = 3;
        await vm.NextMergeStepCommand.ExecuteAsync(null);

        Assert.True(vm.IsMergeStepReport);
        Assert.Contains("未完成授权确认", vm.MergeStatus, StringComparison.Ordinal);
        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public async Task MergeRangeDialog_ComparesActualCoverageAndReplacesInput()
    {
        var directory = Path.Combine(Path.GetTempPath(), "GlyphStash.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var basePath = Path.Combine(directory, "Base.ttf");
        var baseBoldPath = Path.Combine(directory, "Base-Bold.ttf");
        var supplementalPath = Path.Combine(directory, "Patch.ttf");
        await File.WriteAllBytesAsync(basePath, [1], CancellationToken.None);
        await File.WriteAllBytesAsync(baseBoldPath, [1], CancellationToken.None);
        await File.WriteAllBytesAsync(supplementalPath, [1], CancellationToken.None);
        var glyphService = new CapturingGlyphCatalogService();
        glyphService.Coverages[basePath] = CreateCoverage(new DomainUnicodeRange(0x0041, 0x0043), new DomainUnicodeRange(0x0100, 0x0100));
        glyphService.Coverages[supplementalPath] = CreateCoverage(new DomainUnicodeRange(0x0042, 0x0044), new DomainUnicodeRange(0x4E00, 0x4E00));
        var fonts =
            new[]
            {
                CreateFontWithFaces("Base", ("Regular", 400, "Normal", basePath), ("Bold", 700, "Normal", baseBoldPath)),
                CreateFontWithFaces("Patch", ("Regular", 400, "Normal", supplementalPath))
            };
        var vm = ShellViewModelTestFactory.Create(
            ShellViewModelTestFactory.CreateLibraryService(new FakeInventory([]), new FakeStore(fonts)),
            null,
            NullUserFileDialogService.Instance,
            glyphCatalogService: glyphService);

        await vm.InitializeAsync();
        vm.SelectedNavigationItem = vm.NavigationItems.Single(item => item.Key == "merge-tool");
        await vm.OpenMergeRangeDialogCommand.ExecuteAsync(null);

        Assert.True(vm.IsMergeRangeDialogOpen);
        Assert.False(vm.IsMergeRangeDialogBusy);
        Assert.False(vm.HasSelectedMergeRangeSegments);
        Assert.False(vm.CanApplyMergeRangeSelection);
        Assert.Equal(2, glyphService.CoverageQueries.Count);
        Assert.All(glyphService.CoverageQueries, query => Assert.Equal("Regular 400", query.FaceName));
        Assert.Contains(vm.MergeRangeSegments, segment => segment.RangeLabel == "U+0041" && segment.IsBaseOnly);
        Assert.Contains(vm.MergeRangeSegments, segment => segment.RangeLabel == "U+0042-U+0043" && segment.IsBoth);
        Assert.Contains(vm.MergeRangeSegments, segment => segment.RangeLabel == "U+0044" && segment.IsSupplementalOnly);

        var changedProperties = new List<string?>();
        vm.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);
        vm.SelectedMergeRangeBlock = vm.MergeRangeBlocks.Single(block => block.Name == "Basic Latin");

        Assert.Contains(nameof(ShellViewModel.MergeRangeSegments), changedProperties);
        Assert.Contains(vm.MergeRangeSegments, segment => segment.RangeLabel == "U+0041" && segment.IsBaseOnly);
        Assert.DoesNotContain(vm.MergeRangeSegments, segment => segment.RangeLabel == "U+4E00");

        vm.MergeRangeSegments.Single(segment => segment.RangeLabel == "U+0041").IsSelected = true;
        vm.MergeRangeSegments.Single(segment => segment.RangeLabel == "U+0042-U+0043").IsSelected = true;
        Assert.True(vm.HasSelectedMergeRangeSegments);
        Assert.True(vm.CanApplyMergeRangeSelection);
        Assert.Contains("2", vm.MergeRangeSelectedCountLabel, StringComparison.Ordinal);
        vm.ApplyMergeRangeSelectionCommand.Execute(null);

        Assert.False(vm.IsMergeRangeDialogOpen);
        Assert.Equal("U+0041-U+0043", vm.MergeUnicodeRanges);
    }

    [Fact]
    public async Task MergeRangeDialog_ShowsStatusWhenFontsAreMissing()
    {
        var vm = ShellViewModelTestFactory.Create(
            ShellViewModelTestFactory.CreateLibraryService(new FakeInventory([]), new FakeStore([])),
            null,
            NullUserFileDialogService.Instance);

        await vm.OpenMergeRangeDialogCommand.ExecuteAsync(null);

        Assert.True(vm.IsMergeRangeDialogOpen);
        Assert.Contains("请先选择基础字体 A 和补充字体 B", vm.MergeRangeDialogStatus, StringComparison.Ordinal);
        Assert.False(vm.CanApplyMergeRangeSelection);

        vm.ApplyMergeRangeSelectionCommand.Execute(null);

        Assert.Contains("请先选择基础字体 A 和补充字体 B", vm.MergeRangeDialogStatus, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScanFailure_ShowsRootErrorMessage()
    {
        var vm = ShellViewModelTestFactory.Create(ShellViewModelTestFactory.CreateLibraryService(new ThrowingInventory(), new FakeStore([])));

        await vm.InitializeAsync();

        if (!vm.ScanStatus.Contains("fixture scan failed", StringComparison.Ordinal) ||
            !vm.StatusMessage.Contains("fixture scan failed", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Expected scan failure to expose the root error message.");
        }
    }

    [Fact]
    public void InstalledFont_DisablesTemporaryActivation()
    {
        var font = new FontFamilyItemViewModel(CreateFont("Installed Sans", FontActivationState.Installed, FontSourceKind.UserInstalled));

        Assert.False(font.CanTemporarilyActivate);
        Assert.Equal("已安装，无需临时启用", font.TemporaryActivationDisabledReason);
    }

    [Fact]
    public void TemporarilyEnabledFont_CanBeClosed()
    {
        var font = new FontFamilyItemViewModel(CreateFont("Temp Sans", FontActivationState.TemporarilyEnabled, FontSourceKind.GlyphStashManaged));

        Assert.True(font.CanTemporarilyActivate);
        Assert.Equal("关闭临时启用", font.TemporaryActivationLabel);
    }

    [Fact]
    public void ImportInstallForCurrentUser_DisablesTemporaryActivationOption()
    {
        var vm = ShellViewModelTestFactory.Create(ShellViewModelTestFactory.CreateLibraryService(new FakeInventory([]), new FakeStore([])));

        vm.ImportTemporarilyActivate = true;
        vm.ImportInstallForCurrentUser = true;

        Assert.False(vm.ImportTemporarilyActivate);
        Assert.False(vm.CanImportTemporarilyActivate);
        Assert.Equal("用户级安装后已安装，无需临时启用", vm.ImportTemporaryActivationReason);
    }

    [Fact]
    public void TrayHideGuard_BlocksWhileNonFontUiWorkIsBusy()
    {
        var vm = ShellViewModelTestFactory.Create(ShellViewModelTestFactory.CreateLibraryService(new FakeInventory([]), new FakeStore([])));

        Assert.True(vm.CanHideToTray);
        Assert.Equal("", vm.TrayHideBlockReason);

        vm.IsBusy = true;

        Assert.False(vm.CanHideToTray);
        Assert.Contains("任务完成", vm.TrayHideBlockReason, StringComparison.Ordinal);

        vm.IsBusy = false;
        vm.IsMergeBusy = true;

        Assert.False(vm.CanHideToTray);
        Assert.Contains("合并", vm.TrayHideBlockReason, StringComparison.Ordinal);

        vm.IsMergeBusy = false;
        vm.IsGlyphLoading = true;

        Assert.False(vm.CanHideToTray);
        Assert.Contains("字形读取", vm.TrayHideBlockReason, StringComparison.Ordinal);
    }

    [Fact]
    public void RemoteFontStyleOption_FormatsGoogleFontsVariants()
    {
        Assert.Equal("Thin 100", RemoteFontStyleOptionViewModel.FormatVariantLabel("100"));
        Assert.Equal("Regular 400", RemoteFontStyleOptionViewModel.FormatVariantLabel("regular"));
        Assert.Equal("Bold 700", RemoteFontStyleOptionViewModel.FormatVariantLabel("700"));
        Assert.Equal("Bold Italic 700", RemoteFontStyleOptionViewModel.FormatVariantLabel("700italic"));
        Assert.Equal("Regular Italic 400", RemoteFontStyleOptionViewModel.FormatVariantLabel("italic"));
    }

    [Fact]
    public void SelectedFont_DefaultsPreviewFaceToRegular400()
    {
        var font = CreateFontWithFaces("Noto Sans", ("Thin 100", 100, "Normal", "C:/Fonts/NotoSans-Thin.ttf"), ("Regular 400", 400, "Normal", "C:/Fonts/NotoSans-Regular.ttf"));
        var vm = ShellViewModelTestFactory.Create(ShellViewModelTestFactory.CreateLibraryService(new FakeInventory([]), new FakeStore([])));

        vm.SelectedFont = new FontFamilyItemViewModel(font);

        Assert.NotNull(vm.SelectedPreviewFace);
        Assert.Equal("Regular 400", vm.SelectedPreviewFace!.StyleLabel);
    }

    [Fact]
    public void SelectedFont_DefaultsPreviewFaceToFirstWhenRegularMissing()
    {
        var font = CreateFontWithFaces("Noto Sans", ("Thin 100", 100, "Normal", "C:/Fonts/NotoSans-Thin.ttf"), ("Bold 700", 700, "Normal", "C:/Fonts/NotoSans-Bold.ttf"));
        var vm = ShellViewModelTestFactory.Create(ShellViewModelTestFactory.CreateLibraryService(new FakeInventory([]), new FakeStore([])));

        vm.SelectedFont = new FontFamilyItemViewModel(font);

        Assert.NotNull(vm.SelectedPreviewFace);
        Assert.Equal("Thin 100", vm.SelectedPreviewFace!.StyleLabel);
    }

    [Fact]
    public void SelectedPreviewFace_UpdatesPreviewPropertiesAndSelectedBadge()
    {
        var font = new FontFamilyItemViewModel(CreateFontWithFaces("Noto Sans", ("Regular 400", 400, "Normal", "C:/Fonts/NotoSans-Regular.ttf"), ("Bold Italic 700", 700, "Italic", "C:/Fonts/NotoSans-BoldItalic.ttf")));
        var registry = new CapturingFontPreviewRegistry();
        var vm = ShellViewModelTestFactory.Create(
            ShellViewModelTestFactory.CreateLibraryService(new FakeInventory([]), new FakeStore([])),
            null,
            NullUserFileDialogService.Instance,
            fontPreviewRegistry: registry);
        vm.SelectedFont = font;

        var boldItalic = font.Faces.Single(face => face.StyleLabel == "Bold Italic 700");
        vm.SelectedPreviewFace = boldItalic;
        _ = vm.SelectedPreviewFontFamily;

        Assert.Same(boldItalic, registry.LastFace);
        Assert.Equal(FontWeight.Bold, vm.SelectedPreviewFontWeight);
        Assert.Equal(FontStyle.Italic, vm.SelectedPreviewFontStyle);
        Assert.True(boldItalic.IsSelected);
        Assert.False(font.Faces.Single(face => face.StyleLabel == "Regular 400").IsSelected);
    }

    [Fact]
    public async Task OpenGlyphBrowser_UsesSelectedPreviewFace()
    {
        var regularPath = Path.Combine(Path.GetTempPath(), "GlyphStash.Tests", $"{Guid.NewGuid():N}-regular.ttf");
        var boldPath = Path.Combine(Path.GetTempPath(), "GlyphStash.Tests", $"{Guid.NewGuid():N}-bold.ttf");
        Directory.CreateDirectory(Path.GetDirectoryName(regularPath)!);
        await File.WriteAllBytesAsync(regularPath, [0], CancellationToken.None);
        await File.WriteAllBytesAsync(boldPath, [0], CancellationToken.None);
        var font = new FontFamilyItemViewModel(CreateFontWithFaces("Noto Sans", ("Regular 400", 400, "Normal", regularPath), ("Bold 700", 700, "Normal", boldPath)));
        var glyphService = new CapturingGlyphCatalogService();
        var vm = ShellViewModelTestFactory.Create(
            ShellViewModelTestFactory.CreateLibraryService(new FakeInventory([]), new FakeStore([])),
            null,
            NullUserFileDialogService.Instance,
            glyphCatalogService: glyphService);
        vm.SelectedFont = font;
        vm.SelectedPreviewFace = font.Faces.Single(face => face.StyleLabel == "Bold 700");

        await vm.OpenGlyphBrowserCommand.ExecuteAsync(null);

        Assert.NotNull(glyphService.LastQuery);
        Assert.Equal(boldPath, glyphService.LastQuery!.FontFilePath);
        Assert.Equal("Bold 700", glyphService.LastQuery.FaceName);

        vm.SelectedPreviewFace = font.Faces.Single(face => face.StyleLabel == "Regular 400");
        await vm.NextGlyphPageCommand.ExecuteAsync(null);

        Assert.Equal(boldPath, glyphService.LastQuery!.FontFilePath);
        Assert.Equal("Bold 700", glyphService.LastQuery.FaceName);
    }

    [Fact]
    public async Task GlyphSearch_CancelsPreviousLoadAndKeepsLatestResult()
    {
        var regularPath = Path.Combine(Path.GetTempPath(), "GlyphStash.Tests", $"{Guid.NewGuid():N}-regular.ttf");
        Directory.CreateDirectory(Path.GetDirectoryName(regularPath)!);
        await File.WriteAllBytesAsync(regularPath, [0], CancellationToken.None);
        var font = new FontFamilyItemViewModel(CreateFontWithFaces("Noto Sans", ("Regular 400", 400, "Normal", regularPath)));
        var glyphService = new CancelableGlyphCatalogService();
        var vm = ShellViewModelTestFactory.Create(
            ShellViewModelTestFactory.CreateLibraryService(new FakeInventory([]), new FakeStore([])),
            null,
            NullUserFileDialogService.Instance,
            glyphCatalogService: glyphService);
        vm.SelectedFont = font;
        vm.SelectedPreviewFace = font.Faces[0];

        await vm.OpenGlyphBrowserCommand.ExecuteAsync(null);
        vm.GlyphSearchText = "old";
        await glyphService.OldStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        vm.GlyphSearchText = "new";

        await WaitUntilAsync(() => string.Equals(vm.GlyphStatus, "page:new", StringComparison.Ordinal));

        Assert.Contains("old", glyphService.CanceledSearches);
        Assert.Equal("page:new", vm.GlyphStatus);
    }

    [Fact]
    public async Task OpenGlyphBrowser_ShowsUnavailableReasonWhenSelectedFaceHasNoPhysicalPath()
    {
        var font = new FontFamilyItemViewModel(CreateFontWithFaces("Noto Sans", ("Regular 400", 400, "Normal", "installed://Noto%20Sans")));
        var vm = ShellViewModelTestFactory.Create(
            ShellViewModelTestFactory.CreateLibraryService(new FakeInventory([]), new FakeStore([])),
            null,
            NullUserFileDialogService.Instance,
            glyphCatalogService: new CapturingGlyphCatalogService());
        vm.SelectedFont = font;
        vm.SelectedPreviewFace = font.Faces[0];

        await vm.OpenGlyphBrowserCommand.ExecuteAsync(null);

        Assert.Contains("Regular 400", vm.GlyphStatus, StringComparison.Ordinal);
        Assert.Contains("没有可读取的本地文件路径", vm.GlyphStatus, StringComparison.Ordinal);
        Assert.False(vm.IsGlyphBrowserOpen);
    }

    [Fact]
    public async Task ActivateSelectedCollection_SkipsInstalledAndAlreadyEnabledFonts()
    {
        var fonts = new[]
        {
            CreateFont("Installed Sans", FontActivationState.Installed, FontSourceKind.UserInstalled),
            CreateFont("Managed Sans", FontActivationState.NotEnabled, FontSourceKind.GlyphStashManaged),
            CreateFont("Temp Sans", FontActivationState.TemporarilyEnabled, FontSourceKind.GlyphStashManaged)
        };
        var platform = new FakePlatformActivation();
        var service = CreateLocalService(
            platform,
            [new FontCollectionRecord("Website", fonts.Select(font => font.FamilyName).ToList())]);
        var vm = ShellViewModelTestFactory.Create(
            ShellViewModelTestFactory.CreateLibraryService(new FakeInventory([]), new FakeStore(fonts)),
            service,
            NullUserFileDialogService.Instance);

        await vm.InitializeAsync();
        await vm.ActivateSelectedCollectionCommand.ExecuteAsync(null);

        Assert.Equal(1, platform.ActivateCalls);
        Assert.Equal("已安装", vm.CollectionFonts.Single(font => font.FamilyName == "Installed Sans").StateLabel);
        Assert.Equal("已临时启用", vm.CollectionFonts.Single(font => font.FamilyName == "Managed Sans").StateLabel);
        Assert.Contains("跳过 2", vm.ToastMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FontLibrary_FiltersBySelectedTagAndCollection()
    {
        var fonts = new[]
        {
            CreateFont("Inter", tags: ["无衬线", "UI"], collections: ["官网改版"]),
            CreateFont("Serif Sans", tags: ["衬线"], collections: ["书籍"])
        };
        var vm = ShellViewModelTestFactory.Create(ShellViewModelTestFactory.CreateLibraryService(new FakeInventory([]), new FakeStore(fonts)));

        await vm.InitializeAsync();
        vm.SelectedTagFilter = "UI";
        vm.SelectedCollectionFilter = "官网改版";

        Assert.Single(vm.Fonts);
        Assert.Equal("Inter", vm.SelectedFont?.FamilyName);
    }

    [Fact]
    public async Task SaveTagsDialog_UsesExistingSelectionsAndAllowsNewNames()
    {
        var fonts = new[] { CreateFont("Inter", tags: ["无衬线"], collections: []) };
        var tagStore = new FakeTagStore();
        var collectionStore = new FakeCollectionStore([new FontCollectionRecord("官网改版", [])]);
        var mutationStore = new FakeMutationStore(tagStore, collectionStore);
        var platform = new FakePlatformActivation();
        var service = CreateLocalService(
            platform,
            collectionStore.Collections,
            mutationStore: mutationStore,
            collectionStore: collectionStore,
            tagStore: tagStore);
        var vm = ShellViewModelTestFactory.Create(
            ShellViewModelTestFactory.CreateLibraryService(new FakeInventory([]), new FakeStore(fonts)),
            service,
            NullUserFileDialogService.Instance);

        await vm.InitializeAsync();
        vm.OpenTagsDialogCommand.Execute(null);
        vm.TagOptions.Single(option => option.Name == "UI").IsSelected = true;
        vm.CollectionOptions.Single(option => option.Name == "官网改版").IsSelected = true;
        vm.TagEditorText = "项目A";
        vm.CollectionEditorText = "品牌包";
        await vm.SaveTagsDialogCommand.ExecuteAsync(null);

        Assert.Equal(3, mutationStore.Tags.Count);
        Assert.Contains("无衬线", mutationStore.Tags);
        Assert.Contains("UI", mutationStore.Tags);
        Assert.Contains("项目A", mutationStore.Tags);
        Assert.Equal(2, mutationStore.Collections.Count);
        Assert.Contains("官网改版", mutationStore.Collections);
        Assert.Contains("品牌包", mutationStore.Collections);
        Assert.Contains("项目A", vm.SelectedFont?.Tags ?? []);
        Assert.Contains("品牌包", vm.SelectedFont?.Collections ?? []);
    }

    [Fact]
    public async Task SaveTagsDialog_PreservesExistingTagAndCollectionFilters()
    {
        var fonts = new[]
        {
            CreateFont("Inter", tags: ["UI"], collections: ["官网改版"]),
            CreateFont("Serif Sans", tags: ["衬线"], collections: ["书籍"])
        };
        var tagStore = new FakeTagStore([new TagRecord("UI", 1), new TagRecord("衬线", 1)]);
        var collectionStore = new FakeCollectionStore([new FontCollectionRecord("官网改版", ["Inter"]), new FontCollectionRecord("书籍", ["Serif Sans"])]);
        var mutationStore = new FakeMutationStore();
        var platform = new FakePlatformActivation();
        var service = CreateLocalService(
            platform,
            collectionStore.Collections,
            mutationStore: mutationStore,
            collectionStore: collectionStore,
            tagStore: tagStore);
        var vm = ShellViewModelTestFactory.Create(
            ShellViewModelTestFactory.CreateLibraryService(new FakeInventory([]), new FakeStore(fonts)),
            service,
            NullUserFileDialogService.Instance);

        await vm.InitializeAsync();
        vm.SelectedTagFilter = "UI";
        vm.SelectedCollectionFilter = "官网改版";
        vm.OpenTagsDialogCommand.Execute(null);
        await vm.SaveTagsDialogCommand.ExecuteAsync(null);

        Assert.Equal("UI", vm.SelectedTagFilter);
        Assert.Equal("官网改版", vm.SelectedCollectionFilter);
        Assert.Single(vm.Fonts);
        Assert.Equal("Inter", vm.SelectedFont?.FamilyName);
    }

    [Fact]
    public async Task SaveTagsDialog_NotifiesCurrentFiltersAfterReloadingOptions()
    {
        var fonts = new[]
        {
            CreateFont("Inter", tags: ["UI"], collections: ["官网改版"]),
            CreateFont("Serif Sans", tags: ["衬线"], collections: ["书籍"])
        };
        var tagStore = new FakeTagStore([new TagRecord("UI", 1), new TagRecord("衬线", 1)]);
        var collectionStore = new FakeCollectionStore([new FontCollectionRecord("官网改版", ["Inter"]), new FontCollectionRecord("书籍", ["Serif Sans"])]);
        var service = CreateLocalService(
            new FakePlatformActivation(),
            collectionStore.Collections,
            collectionStore: collectionStore,
            tagStore: tagStore);
        var vm = ShellViewModelTestFactory.Create(
            ShellViewModelTestFactory.CreateLibraryService(new FakeInventory([]), new FakeStore(fonts)),
            service,
            NullUserFileDialogService.Instance);
        var notifiedProperties = new List<string?>();

        await vm.InitializeAsync();
        vm.SelectedTagFilter = "UI";
        vm.SelectedCollectionFilter = "官网改版";
        vm.OpenTagsDialogCommand.Execute(null);
        vm.PropertyChanged += (_, args) => notifiedProperties.Add(args.PropertyName);

        await vm.SaveTagsDialogCommand.ExecuteAsync(null);

        Assert.Equal("UI", vm.SelectedTagFilter);
        Assert.Equal("官网改版", vm.SelectedCollectionFilter);
        Assert.Contains(nameof(vm.SelectedTagFilter), notifiedProperties);
        Assert.Contains(nameof(vm.SelectedCollectionFilter), notifiedProperties);
    }

    [Fact]
    public async Task SaveTagsDialog_DoesNotTemporarilyRemoveCurrentFilterOptions()
    {
        var fonts = new[]
        {
            CreateFont("Inter", tags: ["UI"], collections: ["官网改版"]),
            CreateFont("Serif Sans", tags: ["衬线"], collections: ["书籍"])
        };
        var tagStore = new FakeTagStore([new TagRecord("UI", 1), new TagRecord("衬线", 1)]);
        var collectionStore = new FakeCollectionStore([new FontCollectionRecord("官网改版", ["Inter"]), new FontCollectionRecord("书籍", ["Serif Sans"])]);
        var mutationStore = new FakeMutationStore(tagStore, collectionStore);
        var service = CreateLocalService(
            new FakePlatformActivation(),
            collectionStore.Collections,
            mutationStore: mutationStore,
            collectionStore: collectionStore,
            tagStore: tagStore);
        var vm = ShellViewModelTestFactory.Create(
            ShellViewModelTestFactory.CreateLibraryService(new FakeInventory([]), new FakeStore(fonts)),
            service,
            NullUserFileDialogService.Instance);
        var removedSelectedTag = false;
        var removedSelectedCollection = false;

        await vm.InitializeAsync();
        vm.SelectedTagFilter = "UI";
        vm.SelectedCollectionFilter = "官网改版";
        vm.OpenTagsDialogCommand.Execute(null);
        vm.TagEditorText = "项目A";
        vm.CollectionEditorText = "品牌包";
        vm.TagFilters.CollectionChanged += (_, args) => removedSelectedTag |= RemovesFilterOption(args, "UI");
        vm.CollectionFilters.CollectionChanged += (_, args) => removedSelectedCollection |= RemovesFilterOption(args, "官网改版");

        await vm.SaveTagsDialogCommand.ExecuteAsync(null);

        Assert.False(removedSelectedTag);
        Assert.False(removedSelectedCollection);
        Assert.Contains("项目A", vm.TagFilters);
        Assert.Contains("品牌包", vm.CollectionFilters);
    }

    [Fact]
    public async Task SaveTagsDialog_AddsNewFilterNamesWithoutClearingCurrentFilters()
    {
        var fonts = new[]
        {
            CreateFont("Inter", tags: ["UI"], collections: ["官网改版"]),
            CreateFont("Serif Sans", tags: ["衬线"], collections: ["书籍"])
        };
        var tagStore = new FakeTagStore([new TagRecord("UI", 1), new TagRecord("衬线", 1)]);
        var collectionStore = new FakeCollectionStore([new FontCollectionRecord("官网改版", ["Inter"]), new FontCollectionRecord("书籍", ["Serif Sans"])]);
        var mutationStore = new FakeMutationStore(tagStore, collectionStore);
        var platform = new FakePlatformActivation();
        var service = CreateLocalService(
            platform,
            collectionStore.Collections,
            mutationStore: mutationStore,
            collectionStore: collectionStore,
            tagStore: tagStore);
        var vm = ShellViewModelTestFactory.Create(
            ShellViewModelTestFactory.CreateLibraryService(new FakeInventory([]), new FakeStore(fonts)),
            service,
            NullUserFileDialogService.Instance);

        await vm.InitializeAsync();
        vm.SelectedTagFilter = "UI";
        vm.SelectedCollectionFilter = "官网改版";
        vm.OpenTagsDialogCommand.Execute(null);
        vm.TagEditorText = "项目A";
        vm.CollectionEditorText = "品牌包";
        await vm.SaveTagsDialogCommand.ExecuteAsync(null);

        Assert.Contains("项目A", vm.TagFilters);
        Assert.Contains("品牌包", vm.CollectionFilters);
        Assert.Equal("UI", vm.SelectedTagFilter);
        Assert.Equal("官网改版", vm.SelectedCollectionFilter);
    }

    [Fact]
    public async Task ConfirmDeleteTag_RemovesTagAndClearsMatchingFilter()
    {
        var fonts = new[]
        {
            CreateFont("Inter", tags: ["UI"], collections: []),
            CreateFont("Serif Sans", tags: ["UI", "衬线"], collections: [])
        };
        var tagStore = new FakeTagStore([new TagRecord("UI", 2), new TagRecord("衬线", 1)]);
        var platform = new FakePlatformActivation();
        var service = CreateLocalService(
            platform,
            [],
            tagStore: tagStore);
        var vm = ShellViewModelTestFactory.Create(
            ShellViewModelTestFactory.CreateLibraryService(new FakeInventory([]), new FakeStore(fonts)),
            service,
            NullUserFileDialogService.Instance);

        await vm.InitializeAsync();
        vm.SelectedTagFilter = "UI";
        vm.OpenTagsDialogCommand.Execute(null);
        vm.OpenDeleteTagDialogCommand.Execute("UI");
        await vm.ConfirmDeleteTagCommand.ExecuteAsync(null);

        Assert.Contains("UI", tagStore.DeletedTags);
        Assert.False(vm.IsDeleteTagDialogOpen);
        Assert.Equal("", vm.PendingDeleteTagName);
        Assert.Equal("全部标签", vm.SelectedTagFilter);
        Assert.DoesNotContain("UI", vm.TagFilters);
        Assert.All(vm.Fonts, font => Assert.DoesNotContain("UI", font.Tags));
    }

    [Fact]
    public async Task DeleteSelectedCollection_RemovesCollectionAndClearsFilter()
    {
        var collectionStore = new FakeCollectionStore([new FontCollectionRecord("官网改版", ["Inter"])]);
        var platform = new FakePlatformActivation();
        var service = CreateLocalService(
            platform,
            collectionStore.Collections,
            collectionStore: collectionStore);
        var vm = ShellViewModelTestFactory.Create(
            ShellViewModelTestFactory.CreateLibraryService(new FakeInventory([]), new FakeStore([CreateFont("Inter", collections: ["官网改版"])])),
            service,
            NullUserFileDialogService.Instance);

        await vm.InitializeAsync();
        vm.SelectedCollectionFilter = "官网改版";
        vm.OpenDeleteCollectionDialogCommand.Execute(null);
        await vm.ConfirmDeleteCollectionCommand.ExecuteAsync(null);

        Assert.Empty(vm.Collections);
        Assert.Equal("全部集合", vm.SelectedCollectionFilter);
        Assert.False(vm.IsDeleteCollectionDialogOpen);
    }

    [Fact]
    public async Task SearchOnlineFonts_ShowsReadableProviderError()
    {
        var logStore = new FakeOperationLogStore();
        var onlineService = new OnlineFontService(
            new ThrowingFontSourceProvider("Google Fonts 请求无效：Invalid family query"),
            new FakeSettingsStore("api-key"),
            new FakeManagedFontFileStore(),
            new FakeMutationStore(),
            new FakeInstallService(),
            new FontActivationCoordinator(new FakePlatformActivation(), new FakeActivationStore(), logStore),
            logStore,
            new FakeDownloadRecordStore());
        var vm = ShellViewModelTestFactory.Create(
            ShellViewModelTestFactory.CreateLibraryService(new FakeInventory([]), new FakeStore([])),
            null,
            NullUserFileDialogService.Instance,
            onlineService);

        await vm.SearchOnlineFontsCommand.ExecuteAsync(null);

        Assert.Contains("请求无效", vm.OnlineStatus, StringComparison.Ordinal);
        Assert.DoesNotContain("Response status code does not indicate success", vm.OnlineStatus, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchOnlineFonts_ForwardsSelectedFiltersToProvider()
    {
        var logStore = new FakeOperationLogStore();
        var provider = new CapturingFontSourceProvider();
        var onlineService = new OnlineFontService(
            provider,
            new FakeSettingsStore("api-key"),
            new FakeManagedFontFileStore(),
            new FakeMutationStore(),
            new FakeInstallService(),
            new FontActivationCoordinator(new FakePlatformActivation(), new FakeActivationStore(), logStore),
            logStore,
            new FakeDownloadRecordStore());
        var vm = ShellViewModelTestFactory.Create(
            ShellViewModelTestFactory.CreateLibraryService(new FakeInventory([]), new FakeStore([])),
            null,
            NullUserFileDialogService.Instance,
            onlineService);
        vm.OnlineSearchText = "Noto";
        vm.SelectedOnlineSubset = "latin-ext";
        vm.SelectedOnlineCategory = "sans-serif";
        vm.SelectedOnlineSort = "popularity";
        vm.OnlineCapabilityVf = true;
        vm.OnlineCapabilityWoff2 = true;

        await vm.SearchOnlineFontsCommand.ExecuteAsync(null);

        Assert.NotNull(provider.LastQuery);
        Assert.Equal("Noto", provider.LastQuery!.SearchText);
        Assert.Equal("latin-ext", provider.LastQuery.Subset);
        Assert.Equal("sans-serif", provider.LastQuery.Category);
        Assert.Equal("popularity", provider.LastQuery.Sort);
        Assert.Equal(["VF", "WOFF2"], provider.LastQuery.Capabilities);
    }

    [Fact]
    public async Task DownloadSelectedOnlineFont_EnqueuesAndCompletesQueueItem()
    {
        var logStore = new FakeOperationLogStore();
        var provider = new QueueingFontSourceProvider();
        var onlineService = new OnlineFontService(
            provider,
            new FakeSettingsStore("api-key"),
            new FakeManagedFontFileStore(),
            new FakeMutationStore(),
            new FakeInstallService(),
            new FontActivationCoordinator(new FakePlatformActivation(), new FakeActivationStore(), logStore),
            logStore,
            new FakeDownloadRecordStore());
        var vm = ShellViewModelTestFactory.Create(
            ShellViewModelTestFactory.CreateLibraryService(new FakeInventory([]), new FakeStore([])),
            null,
            NullUserFileDialogService.Instance,
            onlineService);
        vm.SelectedRemoteFont = new RemoteFontFamilyItemViewModel(CreateRemoteFont("Noto Sans"));

        await vm.DownloadSelectedOnlineFontCommand.ExecuteAsync(null);

        Assert.Single(vm.OnlineDownloadQueue);
        Assert.True(vm.HasOnlineDownloadQueue);
        Assert.Equal(OnlineFontDownloadStatus.Succeeded, vm.OnlineDownloadQueue[0].Status);
        Assert.Equal(1, provider.DownloadCalls);
        Assert.Contains("成功", vm.OnlineStatus, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RetryOnlineDownload_RerunsFailedQueueItem()
    {
        var logStore = new FakeOperationLogStore();
        var provider = new QueueingFontSourceProvider { FailDownloadCount = 1 };
        var onlineService = new OnlineFontService(
            provider,
            new FakeSettingsStore("api-key"),
            new FakeManagedFontFileStore(),
            new FakeMutationStore(),
            new FakeInstallService(),
            new FontActivationCoordinator(new FakePlatformActivation(), new FakeActivationStore(), logStore),
            logStore,
            new FakeDownloadRecordStore());
        var vm = ShellViewModelTestFactory.Create(
            ShellViewModelTestFactory.CreateLibraryService(new FakeInventory([]), new FakeStore([])),
            null,
            NullUserFileDialogService.Instance,
            onlineService);
        vm.SelectedRemoteFont = new RemoteFontFamilyItemViewModel(CreateRemoteFont("Noto Sans"));

        await vm.DownloadSelectedOnlineFontCommand.ExecuteAsync(null);

        var item = Assert.Single(vm.OnlineDownloadQueue);
        Assert.Equal(OnlineFontDownloadStatus.Failed, item.Status);
        Assert.True(item.CanRetry);
        Assert.Contains("fixture download failed", item.ErrorMessage, StringComparison.Ordinal);

        await vm.RetryOnlineDownloadCommand.ExecuteAsync(item);

        Assert.Equal(OnlineFontDownloadStatus.Succeeded, item.Status);
        Assert.Equal(2, provider.DownloadCalls);
    }

    private static FontFamilyRecord CreateFont(
        string family,
        FontActivationState state = FontActivationState.Installed,
        FontSourceKind sourceKind = FontSourceKind.System,
        IReadOnlyList<string>? tags = null,
        IReadOnlyList<string>? collections = null)
    {
        var file = new FontFileRecord($"C:/Fonts/{family}.ttf", "TTF", null, sourceKind, DateTimeOffset.UtcNow);
        return new FontFamilyRecord(
            family,
            [new FontFaceRecord(family, "Regular", $"{family} Regular", $"{family}-Regular", 400, "Normal", "Normal", file)],
            sourceKind,
            state,
            LicenseStatus.Unknown,
            "未知授权",
            tags ?? ["无衬线"],
            collections ?? [],
            false);
    }

    private static FontFamilyRecord CreateFontWithFaces(string family, params (string Style, int Weight, string Slant, string Path)[] faces) =>
        new(
            family,
            faces.Select(face =>
            {
                var file = new FontFileRecord(face.Path, "TTF", null, FontSourceKind.GlyphStashManaged, DateTimeOffset.UtcNow);
                return new FontFaceRecord(family, face.Style, $"{family} {face.Style}", $"{family.Replace(' ', '-')}-{face.Style.Replace(' ', '-')}", face.Weight, "Normal", face.Slant, file);
            }).ToList(),
            FontSourceKind.GlyphStashManaged,
            FontActivationState.NotEnabled,
            LicenseStatus.Unknown,
            "未知授权",
            [],
            [],
            false);

    private static GlyphCoverage CreateCoverage(params DomainUnicodeRange[] ranges)
    {
        var total = ranges.Sum(range => range.Count);
        return new GlyphCoverage(
            ranges,
            [new GlyphCoverageBlock(UnicodeCoverageBlocks.AllBlocks, 0, 0, total)],
            ranges.Select(range => new GlyphCoverageSegment(
                    range,
                    UnicodeCoverageBlocks.FindKnownBlock(range.Start)?.Name ?? UnicodeCoverageBlocks.OtherCoverage))
                .ToList(),
            total);
    }

    private static RemoteFontFamily CreateRemoteFont(string family) =>
        new(
            "google-fonts",
            family,
            "sans-serif",
            ["latin"],
            "v1",
            null,
            $"https://fonts.google.com/specimen/{family.Replace(' ', '+')}",
            $"请查看来源页面：https://fonts.google.com/specimen/{family.Replace(' ', '+')}",
            [new RemoteFontStyle("regular", "regular.ttf", $"https://fonts.gstatic.com/s/{family.Replace(" ", "", StringComparison.Ordinal).ToLowerInvariant()}/regular.ttf")]);

    private static bool RemovesFilterOption(NotifyCollectionChangedEventArgs args, string option) =>
        args.Action == NotifyCollectionChangedAction.Reset
        || (args.Action is NotifyCollectionChangedAction.Remove or NotifyCollectionChangedAction.Replace
            && (args.OldItems?.Cast<string>().Any(item => string.Equals(item, option, StringComparison.CurrentCultureIgnoreCase)) ?? false));

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(20, timeout.Token);
        }
    }

    private static LocalFontManagementService CreateLocalService(
        FakePlatformActivation platform,
        IReadOnlyList<FontCollectionRecord> collections,
        FakeMutationStore? mutationStore = null,
        FakeCollectionStore? collectionStore = null,
        FakeTagStore? tagStore = null,
        FakeSettingsStore? settingsStore = null)
    {
        var logStore = new FakeOperationLogStore();
        var operationLogger = new OperationLogger(logStore);
        settingsStore ??= new FakeSettingsStore();
        mutationStore ??= new FakeMutationStore();
        collectionStore ??= new FakeCollectionStore(collections);
        tagStore ??= new FakeTagStore();
        var installService = new FakeInstallService();
        var activationCoordinator = new FontActivationCoordinator(platform, new FakeActivationStore(), logStore);
        return new LocalFontManagementService(
            new LocalFontSettingsService(settingsStore, operationLogger),
            new LocalFontImportService(
                new FakeMetadataReader(),
                new FakeManagedFontFileStore(),
                settingsStore,
                new FakeMetadataStore(),
                mutationStore,
                collectionStore,
                installService,
                activationCoordinator,
                operationLogger),
            new LocalFontOrganizationService(mutationStore, tagStore, collectionStore, operationLogger),
            new LocalFontActivationService(installService, activationCoordinator, operationLogger),
            new LocalFontOperationLogService(logStore));
    }

    private sealed class FakeInventory : IFontInventoryService
    {
        private readonly IReadOnlyList<FontFamilyRecord> _fonts;

        public FakeInventory(IReadOnlyList<FontFamilyRecord> fonts)
        {
            _fonts = fonts;
        }

        public Task<IReadOnlyList<FontFamilyRecord>> ScanInstalledFontsAsync(CancellationToken cancellationToken) => Task.FromResult(_fonts);
    }

    private sealed class ThrowingInventory : IFontInventoryService
    {
        public Task<IReadOnlyList<FontFamilyRecord>> ScanInstalledFontsAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("fixture scan failed");
    }

    private sealed class FakeStore : IFontMetadataStore
    {
        private IReadOnlyList<FontFamilyRecord> _fonts;

        public FakeStore(IReadOnlyList<FontFamilyRecord> fonts)
        {
            _fonts = fonts;
        }

        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SaveFontIndexAsync(IReadOnlyList<FontFamilyRecord> fonts, CancellationToken cancellationToken)
        {
            _fonts = fonts;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<FontFamilyRecord>> SearchAsync(FontSearchQuery query, CancellationToken cancellationToken) => Task.FromResult(_fonts);
    }

    private sealed class FakePlatformActivation : ITemporaryFontActivationService
    {
        public int ActivateCalls { get; private set; }

        public Task<ActivationResult> ActivateForCurrentUserSessionAsync(IReadOnlyList<FontFileRef> fonts, CancellationToken cancellationToken)
        {
            ActivateCalls += fonts.Count;
            return Task.FromResult(new ActivationResult(true, fonts.Select(font => new FontActivationFileResult(font, true, 1, 0)).ToList(), "activated"));
        }

        public Task<ActivationResult> DeactivateForCurrentUserSessionAsync(IReadOnlyList<FontFileRef> fonts, CancellationToken cancellationToken) =>
            Task.FromResult(new ActivationResult(true, [], "deactivated"));

        public Task DeactivateAllOwnedActivationsAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeActivationStore : IActivationStore
    {
        public Task<IReadOnlyList<ActivationRecord>> GetOwnedActivationsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ActivationRecord>>([]);

        public Task UpsertActivationAsync(ActivationRecord record, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RemoveActivationAsync(string fontPath, string ownerKey, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task MarkAllOwnedStaleAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeMetadataReader : IFontMetadataReader
    {
        public Task<FontMetadata> ReadMetadataAsync(string fontFilePath, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeManagedFontFileStore : IManagedFontFileStore
    {
        public Task<ManagedFontCopyResult> CopyToManagedDirectoryAsync(string sourcePath, UserFontSettings settings, CancellationToken cancellationToken) =>
            Task.FromResult(new ManagedFontCopyResult(sourcePath, "hash", false));

        public Task<IReadOnlyList<string>> EnumerateManagedFontFilesAsync(UserFontSettings settings, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }

    private sealed class CapturingFontPreviewRegistry : IFontPreviewRegistry
    {
        public FontFaceItemViewModel? LastFace { get; private set; }

        public FontFamily Resolve(FontFaceItemViewModel? face, string fallbackFamilyName)
        {
            LastFace = face;
            return string.IsNullOrWhiteSpace(fallbackFamilyName) ? FontFamily.Default : new FontFamily(fallbackFamilyName);
        }
    }

    private sealed class FakeSettingsStore : IAppSettingsStore
    {
        private UserFontSettings _settings;

        public FakeSettingsStore(string apiKey = "", string uiCultureCode = "")
        {
            _settings = new UserFontSettings("C:/GlyphStash", apiKey, uiCultureCode);
        }

        public int SaveCount { get; private set; }

        public Task<UserFontSettings?> GetSettingsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<UserFontSettings?>(_settings);

        public Task SaveSettingsAsync(UserFontSettings settings, CancellationToken cancellationToken)
        {
            SaveCount++;
            _settings = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingFontSourceProvider : IFontSourceProvider
    {
        private readonly string _message;

        public ThrowingFontSourceProvider(string message)
        {
            _message = message;
        }

        public string ProviderId => "google-fonts";

        public Task<IReadOnlyList<RemoteFontFamily>> SearchAsync(RemoteFontSearchQuery query, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(_message);

        public Task<RemoteFontDownloadResult> DownloadAsync(RemoteFontDownloadRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class CapturingFontSourceProvider : IFontSourceProvider
    {
        public RemoteFontSearchQuery? LastQuery { get; private set; }

        public string ProviderId => "google-fonts";

        public Task<IReadOnlyList<RemoteFontFamily>> SearchAsync(RemoteFontSearchQuery query, CancellationToken cancellationToken)
        {
            LastQuery = query;
            return Task.FromResult<IReadOnlyList<RemoteFontFamily>>([]);
        }

        public Task<RemoteFontDownloadResult> DownloadAsync(RemoteFontDownloadRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class CapturingGlyphCatalogService : IGlyphCatalogService
    {
        public GlyphQuery? LastQuery { get; private set; }

        public List<GlyphCoverageQuery> CoverageQueries { get; } = [];

        public Dictionary<string, GlyphCoverage> Coverages { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<GlyphPage> GetGlyphsAsync(GlyphQuery query, CancellationToken cancellationToken)
        {
            LastQuery = query;
            return Task.FromResult(new GlyphPage([], [new UnicodeBlockOption("全部区块", 0, 0)], query.PageNumber, 120, 240, "empty"));
        }

        public Task<GlyphCoverage> GetCoverageAsync(GlyphCoverageQuery query, CancellationToken cancellationToken)
        {
            CoverageQueries.Add(query);
            return Task.FromResult(Coverages.TryGetValue(query.FontFilePath, out var coverage)
                ? coverage
                : new GlyphCoverage([], [new GlyphCoverageBlock(UnicodeCoverageBlocks.AllBlocks, 0, 0, 0)], [], 0, "empty"));
        }
    }

    private sealed class CancelableGlyphCatalogService : IGlyphCatalogService
    {
        public TaskCompletionSource OldStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<string> CanceledSearches { get; } = [];

        public async Task<GlyphPage> GetGlyphsAsync(GlyphQuery query, CancellationToken cancellationToken)
        {
            if (string.Equals(query.SearchText, "old", StringComparison.Ordinal))
            {
                OldStarted.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    CanceledSearches.Add(query.SearchText);
                    throw;
                }
            }

            return new GlyphPage([], [new UnicodeBlockOption("全部区块", 0, 0)], query.PageNumber, query.PageSize, 0, $"page:{query.SearchText}");
        }

        public Task<GlyphCoverage> GetCoverageAsync(GlyphCoverageQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new GlyphCoverage([], [], [], 0, ""));
    }

    private sealed class FakeMergeWorker : IFontMergeWorker
    {
        public Task<FontMergeWorkerPreviewResult> PreviewAsync(
            FontMergeWorkerRequest request,
            IProgress<FontMergeProgress>? progress,
            CancellationToken cancellationToken) =>
            Task.FromResult(CreatePreview(request.MergeMode));

        public async Task<FontMergeWorkerMergeResult> MergeAsync(
            FontMergeWorkerRequest request,
            IProgress<FontMergeProgress>? progress,
            CancellationToken cancellationToken)
        {
            await File.WriteAllBytesAsync(request.OutputPath, [1, 2, 3], cancellationToken);
            return new FontMergeWorkerMergeResult(CreatePreview(request.MergeMode), request.OutputPath);
        }

        private static FontMergeWorkerPreviewResult CreatePreview(FontMergeMode mergeMode)
        {
            var decision = mergeMode == FontMergeMode.Overwrite
                ? FontMergeDecision.Overwrite
                : FontMergeDecision.SkipDuplicate;
            var note = mergeMode == FontMergeMode.Overwrite
                ? "覆盖基础字体"
                : "基础字体已存在";

            return new FontMergeWorkerPreviewResult(
                [],
                [new FontMergeConflictItem(0x0041, "A", FontMergeCodePointState.Present, FontMergeCodePointState.Present, decision, note)],
                1,
                1,
                0,
                1,
                0,
                mergeMode == FontMergeMode.Overwrite ? 1 : 0);
        }
    }

    private sealed class QueueingFontSourceProvider : IFontSourceProvider
    {
        public int DownloadCalls { get; private set; }

        public int FailDownloadCount { get; init; }

        public string ProviderId => "google-fonts";

        public Task<IReadOnlyList<RemoteFontFamily>> SearchAsync(RemoteFontSearchQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RemoteFontFamily>>([CreateRemoteFont("Noto Sans")]);

        public Task<RemoteFontDownloadResult> DownloadAsync(RemoteFontDownloadRequest request, CancellationToken cancellationToken)
        {
            DownloadCalls++;
            if (DownloadCalls <= FailDownloadCount)
            {
                throw new InvalidOperationException("fixture download failed");
            }

            var style = request.Styles[0];
            var path = $"C:/GlyphStash/{request.Family.FamilyName}-{style.Variant}.ttf";
            return Task.FromResult(new RemoteFontDownloadResult(
                request.Family,
                [new RemoteFontDownloadedFile(style, path, "hash", "TTF")],
                "已下载 1 个样式。"));
        }
    }

    private sealed class FakeDownloadRecordStore : IDownloadRecordStore
    {
        public Task AddDownloadRecordAsync(DownloadRecord record, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<DownloadRecord>> GetRecentDownloadRecordsAsync(int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DownloadRecord>>([]);
    }

    private sealed class FakeMetadataStore : IFontMetadataStore
    {
        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SaveFontIndexAsync(IReadOnlyList<FontFamilyRecord> fonts, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<FontFamilyRecord>> SearchAsync(FontSearchQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FontFamilyRecord>>([]);
    }

    private sealed class FakeMutationStore : IFontLibraryMutationStore
    {
        private readonly FakeTagStore? _tagStore;
        private readonly FakeCollectionStore? _collectionStore;

        public FakeMutationStore(FakeTagStore? tagStore = null, FakeCollectionStore? collectionStore = null)
        {
            _tagStore = tagStore;
            _collectionStore = collectionStore;
        }

        public IReadOnlyList<string> Tags { get; private set; } = [];

        public IReadOnlyList<string> Collections { get; private set; } = [];

        public Task SetFavoriteAsync(string familyName, bool isFavorite, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SetTagsAsync(string familyName, IReadOnlyList<string> tags, CancellationToken cancellationToken)
        {
            Tags = tags;
            _tagStore?.EnsureTags(tags);
            return Task.CompletedTask;
        }

        public Task SetCollectionsAsync(string familyName, IReadOnlyList<string> collections, CancellationToken cancellationToken)
        {
            Collections = collections;
            _collectionStore?.EnsureCollections(collections);
            return Task.CompletedTask;
        }

        public Task UpsertManagedFontAsync(ManagedFontRecord managedFont, FontFamilyRecord family, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeCollectionStore : ICollectionStore
    {
        public FakeCollectionStore(IReadOnlyList<FontCollectionRecord> collections)
        {
            Collections = collections.ToList();
        }

        public List<FontCollectionRecord> Collections { get; }

        public void EnsureCollections(IEnumerable<string> names)
        {
            foreach (var name in names)
            {
                if (Collections.All(collection => !string.Equals(collection.Name, name, StringComparison.CurrentCultureIgnoreCase)))
                {
                    Collections.Add(new FontCollectionRecord(name, []));
                }
            }
        }

        public Task<IReadOnlyList<FontCollectionRecord>> GetCollectionsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FontCollectionRecord>>(Collections);

        public Task CreateCollectionAsync(string name, CancellationToken cancellationToken)
        {
            if (Collections.All(collection => !string.Equals(collection.Name, name, StringComparison.CurrentCultureIgnoreCase)))
            {
                Collections.Add(new FontCollectionRecord(name, []));
            }

            return Task.CompletedTask;
        }

        public Task RenameCollectionAsync(string oldName, string newName, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteCollectionAsync(string name, CancellationToken cancellationToken)
        {
            Collections.RemoveAll(collection => string.Equals(collection.Name, name, StringComparison.CurrentCultureIgnoreCase));
            return Task.CompletedTask;
        }

        public Task AddFontToCollectionAsync(string collectionName, string familyName, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RemoveFontFromCollectionAsync(string collectionName, string familyName, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeTagStore : ITagStore
    {
        public FakeTagStore(IReadOnlyList<TagRecord>? tags = null)
        {
            Tags = tags?.ToList() ?? [new TagRecord("无衬线", 1), new TagRecord("UI", 0)];
        }

        public List<TagRecord> Tags { get; }

        public List<string> DeletedTags { get; } = [];

        public void EnsureTags(IEnumerable<string> names)
        {
            foreach (var name in names)
            {
                if (Tags.All(tag => !string.Equals(tag.Name, name, StringComparison.CurrentCultureIgnoreCase)))
                {
                    Tags.Add(new TagRecord(name, 0));
                }
            }
        }

        public Task<IReadOnlyList<TagRecord>> GetTagsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TagRecord>>(Tags);

        public Task CreateTagAsync(string name, CancellationToken cancellationToken)
        {
            EnsureTags([name]);
            return Task.CompletedTask;
        }

        public Task RenameTagAsync(string oldName, string newName, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteTagAsync(string name, CancellationToken cancellationToken)
        {
            DeletedTags.Add(name);
            Tags.RemoveAll(tag => string.Equals(tag.Name, name, StringComparison.CurrentCultureIgnoreCase));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeInstallService : IFontInstallService
    {
        public Task<FontInstallResult> InstallForCurrentUserAsync(FontFileRef fontFile, CancellationToken cancellationToken) =>
            Task.FromResult(new FontInstallResult(true, fontFile.Path, fontFile.Path, "installed"));

        public Task<FontUninstallResult> UninstallManagedFontAsync(ManagedFontRecord managedFont, CancellationToken cancellationToken) =>
            Task.FromResult(new FontUninstallResult(true, managedFont.ManagedFilePath, "uninstalled"));
    }

    private sealed class FakeAppUpdateService : IAppUpdateService
    {
        private readonly AppUpdateCheckResult _checkResult;

        public FakeAppUpdateService(AppUpdateCheckResult checkResult)
        {
            _checkResult = checkResult;
        }

        public string UpdateSource => "https://github.com/Runarry/GlyphStash";

        public string CurrentVersion => _checkResult.CurrentVersion;

        public AppUpdateApplyResult ApplyResult { get; set; } = new(true, "applied");

        public Task<AppUpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_checkResult);

        public Task<AppUpdateApplyResult> DownloadAndApplyUpdateAsync(IProgress<int>? progress, CancellationToken cancellationToken)
        {
            progress?.Report(42);
            return Task.FromResult(ApplyResult);
        }
    }

    private sealed class FakeOperationLogStore : IOperationLogStore
    {
        public Task AppendOperationAsync(OperationLogEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<OperationLogEntry>> GetRecentOperationsAsync(int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<OperationLogEntry>>([]);
    }
}
