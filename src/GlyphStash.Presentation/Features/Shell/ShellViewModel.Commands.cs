using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using GlyphStash.Application.Fonts;
using GlyphStash.Domain.Fonts;
using GlyphStash.Localization;
using static GlyphStash.Localization.AppTextExtensions;
using DomainUnicodeRange = GlyphStash.Domain.Fonts.UnicodeRange;

namespace GlyphStash.Presentation.ViewModels;

public sealed partial class ShellViewModel
{
    [RelayCommand]
    public async Task InitializeAsync()
    {
        SelectedNavigationItem ??= NavigationItems.First(item => item.Key == "font-library");
        IsBusy = true;
        ScanStatus = L("正在读取 SQLite 字体索引...");
        try
        {
            var settings = await _localManagementService.GetSettingsAsync(LifetimeToken);
            if (!string.IsNullOrWhiteSpace(settings?.UiCultureCode))
            {
                _localizationService.SetCulture(settings.UiCultureCode);
                ApplyCurrentLanguageSelection();
            }

            ManagedFontDirectory = settings?.ManagedFontDirectory ?? "";
            GoogleFontsApiKeyText = settings?.GoogleFontsApiKey ?? "";
            var cached = await _fontLibraryService.LoadCachedFontsAsync(new FontSearchQuery(), LifetimeToken);
            if (cached.Count == 0)
            {
                ScanStatus = L("首次启动：正在扫描 Windows 字体目录...");
                cached = await _fontLibraryService.RescanAsync(LifetimeToken);
            }

            ReplaceFonts(cached);
            await ReloadM2StateAsync();
            StatusMessage = AppText.CurrentCultureCode == AppText.EnglishCultureCode
                ? $"Recent action: loaded {_allFonts.Count:N0} font families"
                : $"最近操作：已加载 {_allFonts.Count:N0} 个字体族";
            ScanStatus = AppText.CurrentCultureCode == AppText.EnglishCultureCode
                ? $"{FontCountLabel}, cache available"
                : $"{FontCountLabel}，缓存可用";
        }
        catch (Exception ex)
        {
            var message = ex.ToUserMessage();
            ScanStatus = AppText.CurrentCultureCode == AppText.EnglishCultureCode ? $"Font index failed to load: {message}" : $"字体索引加载失败：{message}";
            StatusMessage = AppText.CurrentCultureCode == AppText.EnglishCultureCode ? $"Error: {message}" : $"错误：{message}";
            ShowToast(L("字体索引加载失败，可尝试重新扫描"));
        }
        finally
        {
            IsBusy = false;
            NotifyCollectionState();
        }
    }

    [RelayCommand]
    private async Task RescanAsync()
    {
        IsBusy = true;
        ScanStatus = L("正在扫描 C:\\Windows\\Fonts 与用户字体目录...");
        try
        {
            var fonts = await _fontLibraryService.RescanAsync(LifetimeToken);
            SearchText = "";
            SelectedSourceFilter = AllSourceFilter;
            SelectedStateFilter = AllStateFilter;
            SelectedTagFilter = AllTagFilter;
            SelectedCollectionFilter = AllCollectionFilter;
            ReplaceFonts(fonts);
            await ReloadM2StateAsync();
            ScanStatus = AppText.CurrentCultureCode == AppText.EnglishCultureCode
                ? $"Scan complete: {_allFonts.Count:N0} font families, SQLite cache refreshed"
                : $"扫描完成：{_allFonts.Count:N0} 个字体族，SQLite 缓存已刷新";
            StatusMessage = L("最近操作：字体索引已刷新");
            ShowToast(L("字体索引已刷新"));
        }
        catch (Exception ex)
        {
            var message = ex.ToUserMessage();
            ScanStatus = AppText.CurrentCultureCode == AppText.EnglishCultureCode ? $"Scan failed: {message}" : $"扫描失败：{message}";
            StatusMessage = AppText.CurrentCultureCode == AppText.EnglishCultureCode ? $"Error: {message}" : $"错误：{message}";
            ShowToast(L("扫描失败，详情已写入状态栏"));
        }
        finally
        {
            IsBusy = false;
            NotifyCollectionState();
        }
    }

    [RelayCommand]
    private void Navigate(NavigationItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        SelectedNavigationItem = item;
        IsGlyphBrowserOpen = false;
        StatusMessage = item.IsImplemented
            ? FormatCurrentPageStatus(item)
            : FormatPlaceholderPageStatus(item);
    }

    [RelayCommand]
    private void SetMergeStep(MergeStepItemViewModel? step)
    {
        if (step is null || IsMergeBusy)
        {
            return;
        }

        MergeStepIndex = Math.Clamp(step.Index, 0, 4);
    }

    [RelayCommand]
    private async Task NextMergeStepAsync()
    {
        if (IsMergeBusy)
        {
            return;
        }

        if (MergeStepIndex == 0)
        {
            if (SelectedMergeBaseFont is null || SelectedMergeSupplementalFont is null)
            {
                MergeStatus = L("请先选择基础字体 A 和补充字体 B。");
                ShowToast(L("请选择两个字体"));
                return;
            }

            MergeStepIndex = 1;
            return;
        }

        if (MergeStepIndex == 1)
        {
            await PreviewMergeAsync();
            if (_currentMergePreview is { HasBlockingIssues: false })
            {
                MergeStepIndex = 2;
            }

            return;
        }

        if (MergeStepIndex == 2)
        {
            MergeStepIndex = 3;
            return;
        }

        if (MergeStepIndex == 3)
        {
            await StartMergeAsync();
            return;
        }

        ShowToast(L("合并报告已保留"));
    }

    [RelayCommand]
    private void PreviousMergeStep()
    {
        if (CanGoPreviousMergeStep)
        {
            MergeStepIndex--;
        }
    }

    [RelayCommand]
    private void ApplyMergeUnicodeBlock(string block)
    {
        if (string.IsNullOrWhiteSpace(block))
        {
            return;
        }

        MergeUnicodeRanges = block;
        MergeStatus = AppText.CurrentCultureCode == AppText.EnglishCultureCode ? $"Selected Unicode range: {block}" : $"已选择 Unicode 范围：{block}";
    }

    [RelayCommand]
    private async Task OpenMergeRangeDialogAsync()
    {
        IsMergeRangeDialogOpen = true;
        ClearMergeRangeDialog();

        var baseFace = SelectMergeFace(SelectedMergeBaseFont);
        var supplementalFace = SelectMergeFace(SelectedMergeSupplementalFont);
        MergeRangeBaseSummary = BuildPendingMergeRangeSummary(SelectedMergeBaseFont, baseFace, AppText.CurrentCultureCode == AppText.EnglishCultureCode ? "Base font A" : "基础字体 A");
        MergeRangeSupplementalSummary = BuildPendingMergeRangeSummary(SelectedMergeSupplementalFont, supplementalFace, AppText.CurrentCultureCode == AppText.EnglishCultureCode ? "Supplemental font B" : "补充字体 B");
        if (SelectedMergeBaseFont is null || SelectedMergeSupplementalFont is null || baseFace is null || supplementalFace is null)
        {
            MergeRangeDialogStatus = L("请先选择基础字体 A 和补充字体 B。");
            return;
        }

        if (string.IsNullOrWhiteSpace(baseFace.FilePath) || !File.Exists(baseFace.FilePath))
        {
            MergeRangeDialogStatus = AppText.CurrentCultureCode == AppText.EnglishCultureCode
                ? $"Base font A style {baseFace.StyleLabel} has no readable local file path."
                : $"基础字体 A 的样式 {baseFace.StyleLabel} 没有可读取的本地文件路径。";
            return;
        }

        if (string.IsNullOrWhiteSpace(supplementalFace.FilePath) || !File.Exists(supplementalFace.FilePath))
        {
            MergeRangeDialogStatus = AppText.CurrentCultureCode == AppText.EnglishCultureCode
                ? $"Supplemental font B style {supplementalFace.StyleLabel} has no readable local file path."
                : $"补充字体 B 的样式 {supplementalFace.StyleLabel} 没有可读取的本地文件路径。";
            return;
        }

        IsMergeRangeDialogBusy = true;
        MergeRangeDialogStatus = L("正在读取两个字体的 Unicode cmap 覆盖...");
        try
        {
            var baseCoverage = await _glyphCatalogService.GetCoverageAsync(
                new GlyphCoverageQuery(baseFace.FilePath, baseFace.StyleLabel),
                LifetimeToken);
            var supplementalCoverage = await _glyphCatalogService.GetCoverageAsync(
                new GlyphCoverageQuery(supplementalFace.FilePath, supplementalFace.StyleLabel),
                LifetimeToken);

            MergeRangeBaseSummary = BuildCoverageSummary(SelectedMergeBaseFont.FamilyName, baseFace.StyleLabel, baseCoverage);
            MergeRangeSupplementalSummary = BuildCoverageSummary(SelectedMergeSupplementalFont.FamilyName, supplementalFace.StyleLabel, supplementalCoverage);
            BuildMergeRangeComparison(baseCoverage, supplementalCoverage);
            MergeRangeDialogStatus = _allMergeRangeSegments.Count == 0
                ? L("两个字体都没有可选择的 Unicode 映射覆盖。")
                : AppText.CurrentCultureCode == AppText.EnglishCultureCode
                    ? $"Read {_allMergeRangeSegments.Count:N0} actual continuous coverage segments; selecting them replaces the current Unicode range input."
                    : $"已读取 {_allMergeRangeSegments.Count:N0} 个实际连续覆盖段；选择后会替换当前 Unicode 范围输入。";
        }
        catch (Exception ex)
        {
            MergeRangeDialogStatus = ex.ToUserMessage();
        }
        finally
        {
            IsMergeRangeDialogBusy = false;
        }
    }

    [RelayCommand]
    private void CloseMergeRangeDialog()
    {
        IsMergeRangeDialogOpen = false;
    }

    [RelayCommand]
    private void SelectMergeRangeBlock(MergeRangeBlockItemViewModel? block)
    {
        SelectedMergeRangeBlock = block;
    }

    [RelayCommand]
    private void ApplyMergeRangeSelection()
    {
        var ranges = _allMergeRangeSegments
            .Where(segment => segment.IsSelected)
            .Select(segment => segment.Range)
            .ToList();
        if (ranges.Count == 0)
        {
            if (HasLoadedMergeRangeSegments)
            {
                MergeRangeDialogStatus = L("请至少选择一个实际覆盖段。");
            }

            return;
        }

        var normalized = NormalizeRanges(ranges);
        MergeUnicodeRanges = string.Join(", ", normalized.Select(range => range.Label));
        MergeStatus = AppText.CurrentCultureCode == AppText.EnglishCultureCode
            ? $"Selected {normalized.Count:N0} Unicode range segments from actual coverage."
            : $"已从实际覆盖弹窗选择 {normalized.Count:N0} 段 Unicode 范围。";
        IsMergeRangeDialogOpen = false;
    }

    [RelayCommand]
    private async Task PreviewMergeAsync()
    {
        IsMergeBusy = true;
        MergeProgressPercent = 0;
        MergeProgressStage = L("预览");
        MergeProgressMessage = L("正在预检查输入。");
        try
        {
            var preview = await _fontMergeService.PreviewAsync(CreateMergeRequest(includeOutput: false), LifetimeToken);
            ApplyMergePreview(preview);
            MergeStatus = preview.HasBlockingIssues
                ? L("冲突预览存在阻止级问题，请检查提示。")
                : L("冲突预览完成，可继续授权与导出。");
        }
        catch (Exception ex)
        {
            MergeStatus = ex.ToUserMessage();
            ShowToast(L("合并预览失败"));
        }
        finally
        {
            IsMergeBusy = false;
        }
    }

    [RelayCommand]
    private async Task ChooseMergeOutputPathAsync()
    {
        var suggested = BuildSuggestedMergeOutputFileName();
        var path = await _fileDialogService.PickMergeOutputFileAsync(suggested, LifetimeToken);
        if (!string.IsNullOrWhiteSpace(path))
        {
            MergeOutputPath = path;
        }
    }

    [RelayCommand]
    private async Task StartMergeAsync()
    {
        _mergeCancellation?.Dispose();
        _mergeCancellation = CancellationTokenSource.CreateLinkedTokenSource(LifetimeToken);
        IsMergeBusy = true;
        MergeProgressPercent = 0;
        MergeProgressStage = L("准备");
        MergeProgressMessage = L("正在准备合并任务。");
        var progress = new Progress<FontMergeProgress>(item =>
        {
            MergeProgressPercent = item.Percent;
            MergeProgressStage = item.Stage;
            MergeProgressMessage = item.Message;
            OnPropertyChanged(nameof(MergeProgressLabel));
        });

        try
        {
            var result = await _fontMergeService.MergeAsync(
                CreateMergeRequest(includeOutput: true) with { LicenseConfirmedAt = DateTimeOffset.UtcNow },
                progress,
                _mergeCancellation.Token);
            ApplyMergePreview(new FontMergePreview(
                result.Report.Ranges.Select(ParseReportRange).ToList(),
                result.Issues,
                _currentMergePreview?.Conflicts ?? [],
                result.Report.RequestedCodePointCount,
                result.Report.MergedCodePointCount + result.Report.SkippedDuplicateCodePointCount + result.Report.OverwrittenCodePointCount,
                result.Report.MergedCodePointCount,
                result.Report.SkippedDuplicateCodePointCount,
                result.Report.MissingCodePointCount,
                result.Report.OverwrittenCodePointCount,
                BuildMergeModeStrategy(result.Report.MergeMode)));
            ApplyMergeReport(result.Report, result.ReportPath);
            MergeStepIndex = 4;
            MergeStatus = FontMergeResultTextFormatter.FormatSummary(result);
            ShowToast(result.Succeeded ? L("合并导出完成") : L("合并导出失败"));
        }
        catch (Exception ex)
        {
            MergeStatus = ex.ToUserMessage();
            ShowToast(L("合并导出失败"));
        }
        finally
        {
            IsMergeBusy = false;
            _mergeCancellation?.Dispose();
            _mergeCancellation = null;
        }
    }

    [RelayCommand]
    private void CancelMerge()
    {
        _mergeCancellation?.Cancel();
        MergeStatus = L("正在取消合并任务...");
    }

    [RelayCommand]
    private async Task LoadMergeReportAsync()
    {
        var path = await _fileDialogService.PickMergeReportFileAsync(LifetimeToken);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var report = await _fontMergeService.ReadReportAsync(path, LifetimeToken);
            ApplyMergeReport(report, path);
            MergeStepIndex = 4;
            MergeStatus = L("历史合并报告已加载。");
        }
        catch (Exception ex)
        {
            MergeStatus = ex.ToUserMessage();
            ShowToast(L("合并报告加载失败"));
        }
    }

    [RelayCommand]
    private void SetPreviewMode(string mode)
    {
        PreviewMode = mode;
        ShowToast(AppText.CurrentCultureCode == AppText.EnglishCultureCode ? $"Preview mode changed to: {L(mode)}" : $"预览模式已切换为：{mode}");
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync(FontFamilyItemViewModel? font)
    {
        if (font is null)
        {
            return;
        }

        font.IsFavorite = !font.IsFavorite;
        await _localManagementService.SetFavoriteAsync(font.FamilyName, font.IsFavorite, LifetimeToken);

        ShowToast(font.IsFavorite ? L("已加入收藏") : L("已取消收藏"));
    }

    [RelayCommand]
    private async Task OpenImportDialogAsync()
    {
        IsImportDialogOpen = true;
        ImportStatus = HasManagedFontDirectory ? L("请选择字体文件。") : L("导入前必须先选择 GlyphStash 管理目录。");
        ImportPreviewItems.Clear();
        OnPropertyChanged(nameof(HasImportPreview));

        var files = await _fileDialogService.PickFontFilesAsync(LifetimeToken);
        if (files.Count == 0)
        {
            return;
        }

        try
        {
            _currentImportPreview = await _localManagementService.PreviewImportAsync(files, LifetimeToken);
            foreach (var item in _currentImportPreview.Items)
            {
                ImportPreviewItems.Add(new ImportPreviewItemViewModel(item));
            }

            ImportStatus = _currentImportPreview.HasImportableFonts
                ? AppText.CurrentCultureCode == AppText.EnglishCultureCode
                    ? $"Precheck complete: {_currentImportPreview.Items.Count(item => item.CanImport):N0} importable."
                    : $"预检完成：{_currentImportPreview.Items.Count(item => item.CanImport)} 个可导入。"
                : L("没有可导入的字体文件。");
        }
        catch (Exception ex)
        {
            ImportStatus = ex.ToUserMessage();
        }
        finally
        {
            OnPropertyChanged(nameof(HasImportPreview));
        }
    }

    [RelayCommand]
    private void CloseImportDialog() => IsImportDialogOpen = false;

    [RelayCommand]
    private async Task ChooseManagedDirectoryAsync()
    {
        var directory = await _fileDialogService.PickManagedDirectoryAsync(LifetimeToken);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        await _localManagementService.SaveManagedDirectoryAsync(directory, LifetimeToken);
        ManagedFontDirectory = directory;
        ImportStatus = L("管理目录已选择，可以开始导入。");
        await ReloadOperationLogsAsync();
        ShowToast(L("管理目录已更新"));
    }

    [RelayCommand]
    private async Task SaveGoogleFontsApiKeyAsync()
    {
        await _localManagementService.SaveGoogleFontsApiKeyAsync(GoogleFontsApiKeyText, LifetimeToken);
        OnlineStatus = string.IsNullOrWhiteSpace(GoogleFontsApiKeyText)
            ? L("Google Fonts API key 已清空。")
            : L("Google Fonts API key 已保存，可以搜索在线字体。");
        await ReloadOperationLogsAsync();
        ShowToast(L("Google Fonts API key 已保存"));
    }

    [RelayCommand]
    private async Task CheckForAppUpdatesAsync()
    {
        if (IsAppUpdateBusy)
        {
            return;
        }

        IsAppUpdateBusy = true;
        HasPendingAppUpdate = false;
        AppUpdateAvailableVersion = "";
        AppUpdateReleaseNotes = "";
        AppUpdateProgress = 0;
        AppUpdateStatus = L("正在检查应用更新...");
        try
        {
            var result = await _appUpdateService.CheckForUpdatesAsync(LifetimeToken);
            AppUpdateStatus = result.Message;
            if (result.CanDownload)
            {
                HasPendingAppUpdate = true;
                AppUpdateAvailableVersion = result.AvailableVersion ?? "";
                AppUpdateReleaseNotes = string.IsNullOrWhiteSpace(result.ReleaseNotes)
                    ? L("该版本没有发布说明。")
                    : result.ReleaseNotes;
            }

            ShowToast(result.Message);
        }
        finally
        {
            IsAppUpdateBusy = false;
        }
    }

    [RelayCommand]
    private async Task DownloadAndRestartAppUpdateAsync()
    {
        if (!CanDownloadAppUpdate)
        {
            return;
        }

        IsAppUpdateBusy = true;
        AppUpdateProgress = 0;
        AppUpdateStatus = L("正在下载应用更新...");
        try
        {
            var progress = new InlineProgress(value => AppUpdateProgress = value);
            var result = await _appUpdateService.DownloadAndApplyUpdateAsync(progress, LifetimeToken);
            AppUpdateStatus = result.Message;
            if (!result.Succeeded)
            {
                ShowToast(result.Message);
            }
        }
        finally
        {
            IsAppUpdateBusy = false;
        }
    }

    [RelayCommand]
    private void DismissAppUpdate()
    {
        HasPendingAppUpdate = false;
        AppUpdateAvailableVersion = "";
        AppUpdateReleaseNotes = "";
        AppUpdateProgress = 0;
        AppUpdateStatus = L("已暂缓本次应用更新。");
    }

    private sealed class InlineProgress : IProgress<int>
    {
        private readonly Action<int> _report;

        public InlineProgress(Action<int> report)
        {
            _report = report;
        }

        public void Report(int value) => _report(value);
    }

    [RelayCommand]
    private async Task SearchOnlineFontsAsync()
    {
        RemoteFonts.Clear();
        SelectedRemoteFont = null;
        IsOnlineSearchBusy = true;
        OnlineStatus = L("正在搜索 Google Fonts...");
        try
        {
            var results = await _onlineFontService.SearchAsync(
                OnlineSearchText,
                NormalizeOnlineFilter(SelectedOnlineSubset, AllOnlineSubset),
                NormalizeOnlineFilter(SelectedOnlineCategory, AllOnlineCategory),
                BuildOnlineCapabilities(),
                SelectedOnlineSort,
                LifetimeToken);
            foreach (var result in results)
            {
                RemoteFonts.Add(new RemoteFontFamilyItemViewModel(result));
            }

            SelectedRemoteFont = RemoteFonts.FirstOrDefault();
            OnlineStatus = RemoteFonts.Count == 0
                ? L("没有找到匹配的在线字体。")
                : AppText.CurrentCultureCode == AppText.EnglishCultureCode
                    ? $"Search complete: {RemoteFonts.Count:N0} font families."
                    : $"搜索完成：{RemoteFonts.Count:N0} 个字体族。";
        }
        catch (Exception ex)
        {
            OnlineStatus = ex.ToUserMessage();
        }
        finally
        {
            IsOnlineSearchBusy = false;
            OnPropertyChanged(nameof(HasRemoteFonts));
        }
    }

    [RelayCommand]
    private async Task DownloadSelectedOnlineFontAsync()
    {
        if (SelectedRemoteFont is null)
        {
            OnlineStatus = L("请先选择一个在线字体。");
            return;
        }

        var selectedStyles = SelectedRemoteFont.SelectedStyles;
        if (selectedStyles.Count == 0)
        {
            OnlineStatus = L("请选择至少一个可下载样式。");
            return;
        }

        var item = new OnlineFontDownloadQueueItemViewModel(
            SelectedRemoteFont.ToRecord(),
            selectedStyles,
            new OnlineFontImportOptions(ParseNames(DownloadTagsText), ParseNames(DownloadCollectionsText), DownloadFavorite, DownloadInstallForCurrentUser, DownloadTemporarilyActivate));
        OnlineDownloadQueue.Add(item);
        OnPropertyChanged(nameof(HasOnlineDownloadQueue));
        OnlineStatus = AppText.CurrentCultureCode == AppText.EnglishCultureCode ? $"Added to download queue: {item.FamilyName}" : $"已加入下载队列：{item.FamilyName}";
        await ProcessOnlineDownloadQueueAsync();
    }

    [RelayCommand]
    private async Task RetryOnlineDownloadAsync(OnlineFontDownloadQueueItemViewModel? item)
    {
        if (item is null || !item.CanRetry)
        {
            return;
        }

        item.ResetForRetry();
        OnlineStatus = AppText.CurrentCultureCode == AppText.EnglishCultureCode ? $"Requeued download: {item.FamilyName}" : $"已重新加入下载队列：{item.FamilyName}";
        await ProcessOnlineDownloadQueueAsync();
    }

    private async Task ProcessOnlineDownloadQueueAsync()
    {
        if (_isProcessingOnlineDownloadQueue)
        {
            return;
        }

        CancelAndDispose(ref _onlineDownloadCancellation);
        _onlineDownloadCancellation = CancellationTokenSource.CreateLinkedTokenSource(LifetimeToken);
        var cancellationToken = _onlineDownloadCancellation.Token;
        _isProcessingOnlineDownloadQueue = true;
        NotifyTrayHideState();
        try
        {
            while (OnlineDownloadQueue.FirstOrDefault(item => item.Status == OnlineFontDownloadStatus.Queued) is { } item)
            {
                item.MarkDownloading();
                OnlineStatus = AppText.CurrentCultureCode == AppText.EnglishCultureCode
                    ? $"{OnlineDownloadQueue.Count:N0} items in queue, downloading {item.FamilyName}..."
                    : $"队列中 {OnlineDownloadQueue.Count:N0} 项，正在下载 {item.FamilyName}...";
                try
                {
                    var result = await _onlineFontService.DownloadAsync(item.Family, item.Styles, item.Options, cancellationToken);
                    item.MarkSucceeded(result.Message);
                    var cached = await _fontLibraryService.LoadCachedFontsAsync(new FontSearchQuery(), cancellationToken);
                    ReplaceFonts(cached);
                    await ReloadM2StateAsync(cancellationToken: cancellationToken);
                    OnlineStatus = AppText.CurrentCultureCode == AppText.EnglishCultureCode ? $"Download complete: {item.FamilyName}" : $"下载完成：{item.FamilyName}";
                    ShowToast(AppText.CurrentCultureCode == AppText.EnglishCultureCode ? $"Downloaded {item.FamilyName}" : $"已下载 {item.FamilyName}");
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    item.MarkFailed(L("下载已取消。"));
                    OnlineStatus = L("下载队列已取消。");
                    break;
                }
                catch (Exception ex)
                {
                    var message = ex.ToUserMessage();
                    item.MarkFailed(message);
                    OnlineStatus = AppText.CurrentCultureCode == AppText.EnglishCultureCode ? $"Download failed: {item.FamilyName}. {message}" : $"下载失败：{item.FamilyName}。{message}";
                    ShowToast(L("下载失败，可重试"));
                }
            }
        }
        finally
        {
            _isProcessingOnlineDownloadQueue = false;
            CancelAndDispose(ref _onlineDownloadCancellation);
            NotifyTrayHideState();
            UpdateOnlineQueueStatusIfIdle();
        }
    }

    private void UpdateOnlineQueueStatusIfIdle()
    {
        if (_isProcessingOnlineDownloadQueue || OnlineDownloadQueue.Count == 0)
        {
            return;
        }

        var failed = OnlineDownloadQueue.Count(item => item.Status == OnlineFontDownloadStatus.Failed);
        var succeeded = OnlineDownloadQueue.Count(item => item.Status == OnlineFontDownloadStatus.Succeeded);
        OnlineStatus = failed > 0
            ? AppText.CurrentCultureCode == AppText.EnglishCultureCode
                ? $"Download queue complete: {succeeded:N0} succeeded, {failed:N0} failed. Failed items can be retried."
                : $"下载队列完成：{succeeded:N0} 项成功，{failed:N0} 项失败，可重试失败项。"
            : AppText.CurrentCultureCode == AppText.EnglishCultureCode
                ? $"Download queue complete: {succeeded:N0} succeeded."
                : $"下载队列完成：{succeeded:N0} 项成功。";
    }

    [RelayCommand]
    private async Task OpenGlyphBrowserAsync()
    {
        if (SelectedFont is null)
        {
            ShowToast(L("请先选择一个字体"));
            return;
        }

        SelectedPreviewFace ??= SelectDefaultPreviewFace(SelectedFont);
        if (SelectedPreviewFace is null)
        {
            GlyphStatus = L("当前字体没有可读取的样式。");
            ShowToast(GlyphStatus);
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedPreviewFace.FilePath) || !File.Exists(SelectedPreviewFace.FilePath))
        {
            GlyphStatus = AppText.CurrentCultureCode == AppText.EnglishCultureCode
                ? $"Current style {SelectedPreviewFace.StyleLabel} has no readable local file path."
                : $"当前样式 {SelectedPreviewFace.StyleLabel} 没有可读取的本地文件路径。";
            ShowToast(GlyphStatus);
            return;
        }

        _currentGlyphFace = SelectedPreviewFace;
        OnPropertyChanged(nameof(GlyphBrowserTitle));
        OnPropertyChanged(nameof(GlyphBrowserDescription));
        GlyphSearchText = "";
        SelectedUnicodeBlock = "全部区块";
        GlyphPageNumber = 1;
        IsGlyphBrowserOpen = true;
        NotifyNavigationState();
        await LoadGlyphsAsync(BeginLatest(ref _glyphLoadCancellation));
    }

    [RelayCommand]
    private void BackFromGlyphBrowser()
    {
        IsGlyphBrowserOpen = false;
        _currentGlyphFace = null;
        OnPropertyChanged(nameof(GlyphBrowserTitle));
        OnPropertyChanged(nameof(GlyphBrowserDescription));
        NotifyNavigationState();
    }

    [RelayCommand]
    private async Task NextGlyphPageAsync()
    {
        if (GlyphPageNumber >= GlyphTotalPages)
        {
            return;
        }

        GlyphPageNumber++;
        await LoadGlyphsAsync(BeginLatest(ref _glyphLoadCancellation));
    }

    [RelayCommand]
    private async Task PreviousGlyphPageAsync()
    {
        if (GlyphPageNumber <= 1)
        {
            return;
        }

        GlyphPageNumber--;
        await LoadGlyphsAsync(BeginLatest(ref _glyphLoadCancellation));
    }

    [RelayCommand]
    private async Task CopySelectedGlyphCharacterAsync()
    {
        if (SelectedGlyph is null)
        {
            return;
        }

        await _clipboardService.SetTextAsync(SelectedGlyph.Character, LifetimeToken);
        ShowToast(AppText.CurrentCultureCode == AppText.EnglishCultureCode ? $"Copied character: {SelectedGlyph.Character}" : $"已复制字符：{SelectedGlyph.Character}");
    }

    [RelayCommand]
    private async Task CopySelectedGlyphUnicodeAsync()
    {
        if (SelectedGlyph is null)
        {
            return;
        }

        await _clipboardService.SetTextAsync(SelectedGlyph.UnicodeLabel, LifetimeToken);
        ShowToast(AppText.CurrentCultureCode == AppText.EnglishCultureCode ? $"Copied Unicode: {SelectedGlyph.UnicodeLabel}" : $"已复制 Unicode：{SelectedGlyph.UnicodeLabel}");
    }

    [RelayCommand]
    private async Task StartImportAsync()
    {
        if (_currentImportPreview is null)
        {
            ShowToast(L("请先选择字体文件"));
            return;
        }

        if (!HasManagedFontDirectory)
        {
            ShowToast(L("导入前必须先选择管理目录"));
            return;
        }

        try
        {
            IsBusy = true;
            var temporarilyActivate = !ImportInstallForCurrentUser && ImportTemporarilyActivate;
            var result = await _localManagementService.ImportAsync(
                _currentImportPreview,
                new FontImportOptions(ImportInstallForCurrentUser, temporarilyActivate, ParseNames(ImportTagsText), ParseNames(ImportCollectionsText)),
                LifetimeToken);
            var cached = await _fontLibraryService.LoadCachedFontsAsync(new FontSearchQuery(), LifetimeToken);
            ReplaceFonts(cached);
            await ReloadM2StateAsync();
            IsImportDialogOpen = false;
            ShowToast(AppText.CurrentCultureCode == AppText.EnglishCultureCode
                ? $"Import complete: {result.ImportedCount:N0} succeeded, {result.FailedCount:N0} failed"
                : $"导入完成：{result.ImportedCount} 个成功，{result.FailedCount} 个失败");
        }
        catch (Exception ex)
        {
            ImportStatus = ex.ToUserMessage();
            ShowToast(L("导入失败，详情已写入导入窗口"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenTagsDialog()
    {
        if (SelectedFont is null)
        {
            ShowToast(L("请先选择一个字体"));
            return;
        }

        TagEditorText = "";
        CollectionEditorText = "";
        RebuildManagementOptions();
        IsTagsDialogOpen = true;
    }

    [RelayCommand]
    private void CloseTagsDialog()
    {
        IsTagsDialogOpen = false;
        CloseDeleteTagDialog();
    }

    [RelayCommand]
    private async Task SaveTagsDialogAsync()
    {
        if (SelectedFont is null)
        {
            return;
        }

        var tags = MergeNames(TagOptions.Where(option => option.IsSelected).Select(option => option.Name), ParseNames(TagEditorText));
        var collections = MergeNames(CollectionOptions.Where(option => option.IsSelected).Select(option => option.Name), ParseNames(CollectionEditorText));
        var tagFilter = NormalizeFilter(SelectedTagFilter, AllTagFilter);
        var collectionFilter = NormalizeFilter(SelectedCollectionFilter, AllCollectionFilter);
        await _localManagementService.SetTagsAsync(SelectedFont.FamilyName, tags, LifetimeToken);
        await _localManagementService.SetCollectionsAsync(SelectedFont.FamilyName, collections, LifetimeToken);

        SelectedFont.SetTagsAndCollections(tags, collections);
        await ReloadM2StateAsync(tagFilter, collectionFilter);
        ApplyFilters();
        IsTagsDialogOpen = false;
        ShowToast(L("标签和集合已更新"));
    }

    [RelayCommand]
    private void OpenDeleteTagDialog(string? tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return;
        }

        PendingDeleteTagName = tagName;
        IsDeleteTagDialogOpen = true;
    }

    [RelayCommand]
    private void CloseDeleteTagDialog()
    {
        IsDeleteTagDialogOpen = false;
        PendingDeleteTagName = "";
    }

    [RelayCommand]
    private async Task ConfirmDeleteTagAsync()
    {
        if (string.IsNullOrWhiteSpace(PendingDeleteTagName))
        {
            CloseDeleteTagDialog();
            return;
        }

        var deletedName = PendingDeleteTagName;
        await _localManagementService.DeleteTagAsync(deletedName, LifetimeToken);
        foreach (var font in _allFonts)
        {
            font.RemoveTag(deletedName);
        }

        if (string.Equals(SelectedTagFilter, deletedName, StringComparison.CurrentCultureIgnoreCase))
        {
            SelectedTagFilter = AllTagFilter;
        }

        CloseDeleteTagDialog();
        await ReloadTagsAsync(SelectedTagFilter);
        await ReloadOperationLogsAsync();
        RebuildManagementOptions();
        ApplyFilters();
        ShowToast(L("标签已删除，字体文件不会被删除"));
    }

    [RelayCommand]
    private void OpenUninstallDialog() => IsUninstallDialogOpen = true;

    [RelayCommand]
    private void CloseUninstallDialog() => IsUninstallDialogOpen = false;

    [RelayCommand]
    private async Task ConfirmUninstallAsync()
    {
        if (SelectedFont is null)
        {
            IsUninstallDialogOpen = false;
            return;
        }

        var result = await _localManagementService.UninstallManagedFontAsync(SelectedFont.ToRecord(), LifetimeToken);
        IsUninstallDialogOpen = false;
        ShowToast(result.Message);
        await ReloadOperationLogsAsync();
    }

    [RelayCommand]
    private async Task InstallSelectedFontAsync()
    {
        if (SelectedFont is null)
        {
            return;
        }

        var result = await _localManagementService.InstallFontAsync(SelectedFont.ToRecord(), LifetimeToken);
        if (result.Succeeded)
        {
            SelectedFont.SetActivationState(FontActivationState.Installed);
        }

        ShowToast(result.Message);
        await ReloadOperationLogsAsync();
    }

    [RelayCommand]
    private async Task ToggleTemporaryActivationAsync(FontFamilyItemViewModel? font)
    {
        font ??= SelectedFont;
        if (font is null)
        {
            return;
        }

        if (!font.CanTemporarilyActivate)
        {
            ShowToast(font.TemporaryActivationDisabledReason);
            return;
        }

        try
        {
            var owner = $"font:{font.FamilyName}";
            var wasTemporarilyEnabled = font.ActivationState == FontActivationState.TemporarilyEnabled;
            var result = wasTemporarilyEnabled
                ? await _localManagementService.DeactivateFontAsync(owner, font.ToRecord(), LifetimeToken)
                : await _localManagementService.ActivateFontAsync(owner, font.ToRecord(), LifetimeToken);

            if (result.Succeeded)
            {
                font.SetActivationState(wasTemporarilyEnabled ? FontActivationState.NotEnabled : FontActivationState.TemporarilyEnabled);
            }

            ShowToast(result.Message);
            await ReloadM2StateAsync();
        }
        catch (Exception ex)
        {
            ShowToast(ex.ToUserMessage());
        }
    }

    [RelayCommand]
    private async Task CreateCollectionAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCollectionName))
        {
            return;
        }

        await _localManagementService.CreateCollectionAsync(NewCollectionName, LifetimeToken);
        NewCollectionName = "";
        await ReloadCollectionsAsync();
        ApplyFilters();
        ShowToast(L("集合已创建"));
    }

    [RelayCommand]
    private void OpenDeleteCollectionDialog()
    {
        if (SelectedCollection is null)
        {
            ShowToast(L("请先选择一个集合"));
            return;
        }

        IsDeleteCollectionDialogOpen = true;
    }

    [RelayCommand]
    private void CloseDeleteCollectionDialog() => IsDeleteCollectionDialogOpen = false;

    [RelayCommand]
    private async Task ConfirmDeleteCollectionAsync()
    {
        if (SelectedCollection is null)
        {
            IsDeleteCollectionDialogOpen = false;
            return;
        }

        var deletedName = SelectedCollection.Name;
        await _localManagementService.DeleteCollectionAsync(deletedName, LifetimeToken);
        if (SelectedCollectionFilter == deletedName)
        {
            SelectedCollectionFilter = AllCollectionFilter;
        }

        IsDeleteCollectionDialogOpen = false;
        await ReloadCollectionsAsync();
        await ReloadOperationLogsAsync();
        ApplyFilters();
        ShowToast(L("集合已删除，字体文件不会被删除"));
    }

    [RelayCommand]
    private async Task RemoveFontFromCollectionAsync(FontFamilyItemViewModel? font)
    {
        if (font is null || SelectedCollection is null)
        {
            return;
        }

        await _localManagementService.RemoveFontFromCollectionAsync(SelectedCollection.Name, font.FamilyName, LifetimeToken);
        await ReloadCollectionsAsync();
        ShowToast(L("已从集合移除字体"));
    }

    [RelayCommand]
    private async Task ActivateSelectedCollectionAsync()
    {
        if (SelectedCollection is null)
        {
            return;
        }

        var activatedCount = 0;
        var skippedCount = 0;
        foreach (var font in CollectionFonts)
        {
            if (!font.CanTemporarilyActivate || font.ActivationState != FontActivationState.NotEnabled)
            {
                var reason = font.TemporaryActivationDisabledReason;
                if (string.IsNullOrWhiteSpace(reason))
                {
                    reason = L("当前状态不需要临时启用");
                }

                await _localManagementService.RecordTemporaryActivationSkippedAsync(
                    $"collection:{SelectedCollection.Name}",
                    font.ToRecord(),
                    reason,
                    LifetimeToken);
                skippedCount++;
                continue;
            }

            var result = await _localManagementService.ActivateFontAsync($"collection:{SelectedCollection.Name}", font.ToRecord(), LifetimeToken);
            if (result.Succeeded)
            {
                font.SetActivationState(FontActivationState.TemporarilyEnabled);
                activatedCount++;
            }
            else
            {
                skippedCount++;
            }
        }

        await ReloadM2StateAsync();
        ShowToast(AppText.CurrentCultureCode == AppText.EnglishCultureCode
            ? $"Collection activated temporarily: {activatedCount:N0}; skipped {skippedCount:N0} installed or unavailable fonts"
            : $"集合临时启用：{activatedCount} 个，跳过 {skippedCount} 个已安装或不可启用字体");
    }

    [RelayCommand]
    private async Task DeactivateSelectedCollectionAsync()
    {
        if (SelectedCollection is null)
        {
            return;
        }

        foreach (var font in CollectionFonts)
        {
            await _localManagementService.DeactivateFontAsync($"collection:{SelectedCollection.Name}", font.ToRecord(), LifetimeToken);
            if (font.ActivationState == FontActivationState.TemporarilyEnabled)
            {
                font.SetActivationState(FontActivationState.NotEnabled);
            }
        }

        await ReloadM2StateAsync();
        ShowToast(L("集合持有的临时启用引用已释放"));
    }

    [RelayCommand]
    private void ExportSelectedCollection()
    {
        if (SelectedCollection is null)
        {
            return;
        }

        StatusMessage = AppText.CurrentCultureCode == AppText.EnglishCultureCode
            ? $"Collection manifest ready: {SelectedCollection.Name}, {CollectionFonts.Count:N0} fonts."
            : $"集合清单已准备：{SelectedCollection.Name}，包含 {CollectionFonts.Count:N0} 个字体。";
        ShowToast(L("集合清单已导出为 CSV，包含字体、样式、状态和 license"));
    }

}
