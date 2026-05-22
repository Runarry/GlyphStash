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
    private CancellationToken LifetimeToken => _lifetimeCancellation.Token;

    private async Task SaveSelectedLanguageAsync(LanguageOptionViewModel language, CancellationToken cancellationToken)
    {
        try
        {
            await _localManagementService.SaveUiCultureAsync(language.CultureCode, cancellationToken);
            await ReloadOperationLogsAsync(cancellationToken);

            ShowToast(T("Toast.LanguageChangedFormat", language.DisplayName));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ShowToast(ex.ToUserMessage());
        }
    }

    private void ApplyCurrentLanguageSelection()
    {
        _selectedLanguage = LanguageOptions.FirstOrDefault(language =>
            string.Equals(language.CultureCode, _localizationService.CurrentCulture.Name, StringComparison.OrdinalIgnoreCase));
        OnPropertyChanged(nameof(SelectedLanguage));
    }

    private void RefreshLocalizedState()
    {
        RefreshLocalizedOptions(SourceFilterOptions);
        RefreshLocalizedOptions(StateFilterOptions);
        RefreshLocalizedOptions(TagFilterOptions);
        RefreshLocalizedOptions(CollectionFilterOptions);
        RefreshLocalizedOptions(OnlineSubsetOptionModels);
        RefreshLocalizedOptions(OnlineCategoryOptionModels);
        RefreshLocalizedOptions(MergeModeOptionModels);
        foreach (var item in NavigationItems)
        {
            item.RefreshLocalizedState();
        }

        foreach (var step in MergeSteps)
        {
            step.RefreshLocalizedState();
        }

        foreach (var font in _allFonts)
        {
            font.RefreshLocalizedState();
        }

        foreach (var collection in Collections)
        {
            collection.RefreshLocalizedState();
        }

        foreach (var option in TagOptions.Concat(CollectionOptions))
        {
            option.RefreshLocalizedState();
        }

        foreach (var item in ImportPreviewItems)
        {
            item.RefreshLocalizedState();
        }

        foreach (var item in RemoteFonts)
        {
            item.RefreshLocalizedState();
        }

        foreach (var item in OnlineDownloadQueue)
        {
            item.RefreshLocalizedState();
        }

        foreach (var item in MergeConflicts)
        {
            item.RefreshLocalizedState();
        }

        foreach (var item in MergeIssues)
        {
            item.RefreshLocalizedState();
        }

        foreach (var item in _allMergeRangeSegments)
        {
            item.RefreshLocalizedState();
        }

        OnPropertyChanged(nameof(CurrentPageTitle));
        OnPropertyChanged(nameof(CurrentPageDescription));
        OnPropertyChanged(nameof(FontCountLabel));
        OnPropertyChanged(nameof(EmptyStateMessage));
        OnPropertyChanged(nameof(GlyphBrowserTitle));
        OnPropertyChanged(nameof(GlyphBrowserDescription));
        OnPropertyChanged(nameof(ImportTemporaryActivationReason));
        OnPropertyChanged(nameof(MergeModeDescription));
        OnPropertyChanged(nameof(MergeRangeSelectedCountLabel));
        OnPropertyChanged(nameof(MergeNextActionLabel));
        OnPropertyChanged(nameof(MergePreviewSummary));
        OnPropertyChanged(nameof(MergeDefaultStrategy));
        OnPropertyChanged(nameof(ManagedDirectoryStatus));
        OnPropertyChanged(nameof(GoogleFontsApiKeyStatus));
        OnPropertyChanged(nameof(PendingDeleteTagConfirmationText));
        OnPropertyChanged(nameof(CompatibilityMatrix));
        OnPropertyChanged(nameof(SelectedLanguage));
        RefreshDefaultPreviewText();
        RefreshLocalizedStoredMessages();
    }

    private void RefreshDefaultPreviewText()
    {
        if (string.Equals(PreviewText, DefaultPreviewTextZh, StringComparison.Ordinal)
            || string.Equals(PreviewText, DefaultPreviewTextEn, StringComparison.Ordinal))
        {
            PreviewText = AppText.TranslateLiteral(DefaultPreviewTextZh);
        }
    }

    private void RefreshLocalizedStoredMessages()
    {
        ScanStatus = RefreshScanStatusText(ScanStatus);
        StatusMessage = RefreshStatusMessageText(StatusMessage);
        ImportStatus = LocalizeStoredLiteral(ImportStatus);
        OnlineStatus = LocalizeStoredLiteral(OnlineStatus);
        GlyphStatus = LocalizeStoredLiteral(GlyphStatus);
        MergeStatus = LocalizeStoredLiteral(MergeStatus);
        MergeRangeDialogStatus = LocalizeStoredLiteral(MergeRangeDialogStatus);
        MergeRangeBaseSummary = LocalizeStoredLiteral(MergeRangeBaseSummary);
        MergeRangeSupplementalSummary = LocalizeStoredLiteral(MergeRangeSupplementalSummary);
        MergeReportTitle = LocalizeStoredLiteral(MergeReportTitle);
        MergeReportBadge = LocalizeStoredLiteral(MergeReportBadge);
    }

    private string RefreshScanStatusText(string value)
    {
        if (_allFonts.Count > 0
            && (value.Contains("缓存可用", StringComparison.Ordinal)
                || value.Contains("cache available", StringComparison.OrdinalIgnoreCase)))
        {
            return AppText.CurrentCultureCode == AppText.EnglishCultureCode
                ? $"{FontCountLabel}, cache available"
                : $"{FontCountLabel}，缓存可用";
        }

        if (_allFonts.Count > 0
            && (value.StartsWith("扫描完成", StringComparison.Ordinal)
                || value.StartsWith("Scan complete", StringComparison.Ordinal)))
        {
            return AppText.CurrentCultureCode == AppText.EnglishCultureCode
                ? $"Scan complete: {_allFonts.Count:N0} font families, SQLite cache refreshed"
                : $"扫描完成：{_allFonts.Count:N0} 个字体族，SQLite 缓存已刷新";
        }

        return LocalizeStoredLiteral(value);
    }

    private string RefreshStatusMessageText(string value)
    {
        if (_allFonts.Count > 0
            && (value.StartsWith("最近操作：已加载", StringComparison.Ordinal)
                || value.StartsWith("Recent action: loaded", StringComparison.Ordinal)))
        {
            return AppText.CurrentCultureCode == AppText.EnglishCultureCode
                ? $"Recent action: loaded {_allFonts.Count:N0} font families"
                : $"最近操作：已加载 {_allFonts.Count:N0} 个字体族";
        }

        return LocalizeStoredLiteral(value);
    }

    private static string LocalizeStoredLiteral(string value) =>
        string.IsNullOrWhiteSpace(value) ? value : AppText.TranslateLiteral(value);

    private static string T(string key) => AppText.Get(key);

    private static string T(string key, params object?[] args) => AppText.Format(key, args);

    private static string FormatCurrentPageStatus(NavigationItemViewModel item) =>
        AppText.CurrentCultureCode == AppText.EnglishCultureCode ? $"Current page: {item.Title}" : $"当前页面：{item.Title}";

    private static string FormatPlaceholderPageStatus(NavigationItemViewModel item) =>
        AppText.CurrentCultureCode == AppText.EnglishCultureCode
            ? $"{item.Title} belongs to {item.Milestone}; only the placeholder page is shown"
            : $"{item.Title} 属于 {item.Milestone}，当前仅显示占位页";

    private void InitializeLocalizedOptions()
    {
        ReplaceLocalizedOptions(SourceFilterOptions, SourceFilters);
        ReplaceLocalizedOptions(StateFilterOptions, StateFilters);
        ReplaceLocalizedOptions(TagFilterOptions, TagFilters);
        ReplaceLocalizedOptions(CollectionFilterOptions, CollectionFilters);
        ReplaceLocalizedOptions(OnlineSubsetOptionModels, OnlineSubsetOptions);
        ReplaceLocalizedOptions(OnlineCategoryOptionModels, OnlineCategoryOptions);
        ReplaceMergeModeOptions();
    }

    private static void ReplaceLocalizedOptions(ObservableCollection<LocalizedOptionViewModel> target, IEnumerable<string> values)
    {
        target.Clear();
        foreach (var value in values)
        {
            target.Add(new LocalizedOptionViewModel(value, value));
        }
    }

    private void ReplaceMergeModeOptions()
    {
        MergeModeOptionModels.Clear();
        MergeModeOptionModels.Add(new LocalizedOptionViewModel(SupplementMergeModeLabel, SupplementMergeModeLabel, "Supplement (append)"));
        MergeModeOptionModels.Add(new LocalizedOptionViewModel(OverwriteMergeModeLabel, OverwriteMergeModeLabel, "Overwrite"));
    }

    private static void RefreshLocalizedOptions(IEnumerable<LocalizedOptionViewModel> options)
    {
        foreach (var option in options)
        {
            option.RefreshLocalizedState();
        }
    }

    private void ReplaceFonts(IReadOnlyList<FontFamilyRecord> records)
    {
        _allFonts.Clear();
        _allFonts.AddRange(records.Select(record =>
        {
            var item = new FontFamilyItemViewModel(record);
            item.SetPreview(PreviewText, PreviewFontSize);
            return item;
        }));
        ApplyFilters();
        SelectedFont ??= Fonts.FirstOrDefault();
        RefreshCollectionFonts();
        RefreshMergeFontLists();
        OnPropertyChanged(nameof(FontCountLabel));
    }

    private async Task LoadGlyphsAsync(CancellationToken cancellationToken)
    {
        if (!IsGlyphBrowserOpen)
        {
            return;
        }

        if (_currentGlyphFace is null)
        {
            GlyphStatus = L("当前字体没有可读取的样式。");
            return;
        }

        try
        {
            IsGlyphLoading = true;
            GlyphStatus = L("正在读取字体字形...");
            var page = await _glyphCatalogService.GetGlyphsAsync(
                new GlyphQuery(
                    _currentGlyphFace.FilePath,
                    _currentGlyphFace.StyleLabel,
                    GlyphSearchText,
                    SelectedUnicodeBlock,
                    false,
                    GlyphPageNumber,
                    120),
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            Glyphs.Clear();
            foreach (var glyph in page.Glyphs)
            {
                Glyphs.Add(new GlyphItemViewModel(glyph));
            }

            ReplaceFilterOptions(UnicodeBlocks, page.Blocks.Select(block => block.Name).ToList());
            if (!UnicodeBlocks.Contains(SelectedUnicodeBlock))
            {
                SelectedUnicodeBlock = "全部区块";
            }

            GlyphTotalPages = page.TotalPages;
            SelectedGlyph = Glyphs.FirstOrDefault();
            GlyphStatus = string.IsNullOrWhiteSpace(page.EmptyMessage)
                ? AppText.CurrentCultureCode == AppText.EnglishCultureCode
                    ? $"Showing {Glyphs.Count:N0} / {page.TotalCount:N0} Unicode-mapped glyphs."
                    : $"当前显示 {Glyphs.Count:N0} / {page.TotalCount:N0} 个 Unicode 映射字形。"
                : page.EmptyMessage;
            OnPropertyChanged(nameof(HasGlyphs));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            GlyphStatus = ex.ToUserMessage();
        }
        finally
        {
            IsGlyphLoading = false;
        }
    }

    private void ApplyFilters()
    {
        var selectedName = SelectedFont?.FamilyName;
        Fonts.Clear();
        foreach (var font in _allFonts.Where(font => font.Matches(SearchText, SelectedSourceFilter, SelectedStateFilter, SelectedTagFilter, SelectedCollectionFilter)))
        {
            Fonts.Add(font);
        }

        SelectedFont = Fonts.FirstOrDefault(font => font.FamilyName == selectedName) ?? Fonts.FirstOrDefault();
        NotifyCollectionState();
    }

    private void ApplyCollectionFilter()
    {
        RunLatestAsync(
            ref _collectionReloadCancellation,
            cancellationToken => ReloadCollectionsAsync(cancellationToken: cancellationToken),
            ex => StatusMessage = ex.ToUserMessage());
    }

    private async Task ReloadM2StateAsync(string? preferredTagFilter = null, string? preferredCollectionFilter = null, CancellationToken cancellationToken = default)
    {
        cancellationToken = ResolveCancellationToken(cancellationToken);
        await ReloadTagsAsync(preferredTagFilter, cancellationToken);
        await ReloadCollectionsAsync(preferredCollectionFilter, cancellationToken);
        await ReloadOperationLogsAsync(cancellationToken);
    }

    private async Task ReloadTagsAsync(string? preferredFilter = null, CancellationToken cancellationToken = default)
    {
        cancellationToken = ResolveCancellationToken(cancellationToken);
        var selectedTag = NormalizeFilter(preferredFilter ?? SelectedTagFilter, AllTagFilter);
        var tags = await _localManagementService.GetTagsAsync(cancellationToken);
        AvailableTags.Clear();
        foreach (var tag in tags)
        {
            AvailableTags.Add(tag);
        }

        ReplaceFilterOptions(
            TagFilters,
            [AllTagFilter, .. AvailableTags.Select(tag => tag.Name).OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)]);
        ReplaceLocalizedOptions(TagFilterOptions, TagFilters);

        SelectTagFilter(selectedTag);
    }

    private async Task ReloadCollectionsAsync(string? preferredFilter = null, CancellationToken cancellationToken = default)
    {
        cancellationToken = ResolveCancellationToken(cancellationToken);
        var selectedName = SelectedCollection?.Name;
        var collections = await _localManagementService.GetCollectionsAsync(cancellationToken);
        var selectedFilter = NormalizeFilter(preferredFilter ?? SelectedCollectionFilter, AllCollectionFilter);
        AvailableCollections.Clear();
        var collectionNames = collections
            .Select(collection => collection.Name)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        foreach (var collectionName in collectionNames)
        {
            AvailableCollections.Add(collectionName);
        }

        ReplaceFilterOptions(CollectionFilters, [AllCollectionFilter, .. collectionNames]);
        ReplaceLocalizedOptions(CollectionFilterOptions, CollectionFilters);

        SelectCollectionFilter(selectedFilter);

        Collections.Clear();
        foreach (var collection in collections.Where(collection =>
                     string.IsNullOrWhiteSpace(CollectionSearchText)
                     || collection.Name.Contains(CollectionSearchText, StringComparison.CurrentCultureIgnoreCase)))
        {
            Collections.Add(new CollectionItemViewModel(collection));
        }

        SelectedCollection = Collections.FirstOrDefault(collection => collection.Name == selectedName) ?? Collections.FirstOrDefault();
        RefreshCollectionFonts();
        OnPropertyChanged(nameof(HasCollections));
        OnPropertyChanged(nameof(HasSelectedCollection));
    }

    private async Task ReloadOperationLogsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken = ResolveCancellationToken(cancellationToken);
        OperationLogs.Clear();
        var logs = await _localManagementService.GetRecentOperationsAsync(8, cancellationToken);
        foreach (var log in logs)
        {
            OperationLogs.Add(new OperationLogItemViewModel(log));
        }
    }

    private void RefreshCollectionFonts()
    {
        CollectionFonts.Clear();
        if (SelectedCollection is null)
        {
            return;
        }

        var names = SelectedCollection.FamilyNames.ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        foreach (var font in _allFonts.Where(font => names.Contains(font.FamilyName)))
        {
            CollectionFonts.Add(font);
        }
    }

    private void RefreshMergeFontLists()
    {
        var selectedBase = SelectedMergeBaseFont?.FamilyName;
        var selectedSupplemental = SelectedMergeSupplementalFont?.FamilyName;
        ReplaceMergeFontList(MergeBaseFonts, MergeBaseSearchText, selectedBase, value => SelectedMergeBaseFont = value);
        ReplaceMergeFontList(MergeSupplementalFonts, MergeSupplementalSearchText, selectedSupplemental, value => SelectedMergeSupplementalFont = value);
        if (SelectedMergeBaseFont is null && MergeBaseFonts.Count > 0)
        {
            SelectedMergeBaseFont = MergeBaseFonts[0];
        }

        if (SelectedMergeSupplementalFont is null && MergeSupplementalFonts.Count > 0)
        {
            SelectedMergeSupplementalFont = MergeSupplementalFonts.FirstOrDefault(font => !ReferenceEquals(font, SelectedMergeBaseFont)) ?? MergeSupplementalFonts[0];
        }
        else if (SelectedMergeBaseFont is not null
                 && SelectedMergeSupplementalFont is not null
                 && string.Equals(SelectedMergeBaseFont.FamilyName, SelectedMergeSupplementalFont.FamilyName, StringComparison.CurrentCultureIgnoreCase))
        {
            SelectedMergeSupplementalFont = MergeSupplementalFonts.FirstOrDefault(font => !string.Equals(font.FamilyName, SelectedMergeBaseFont.FamilyName, StringComparison.CurrentCultureIgnoreCase))
                                            ?? SelectedMergeSupplementalFont;
        }
    }

    private void ClearMergeRangeDialog()
    {
        _allMergeRangeSegments.Clear();
        MergeRangeBlocks.Clear();
        SelectedMergeRangeBlock = null;
        MergeRangeBaseSummary = L("未读取基础字体 A。");
        MergeRangeSupplementalSummary = L("未读取补充字体 B。");
        MergeRangeDialogStatus = L("请选择基础字体和补充字体。");
        NotifyMergeRangeDialogState();
    }

    private void BuildMergeRangeComparison(GlyphCoverage baseCoverage, GlyphCoverage supplementalCoverage)
    {
        _allMergeRangeSegments.Clear();
        MergeRangeBlocks.Clear();

        foreach (var segment in BuildComparisonSegments(baseCoverage.Ranges, supplementalCoverage.Ranges))
        {
            var item = new MergeRangeSegmentItemViewModel(segment);
            item.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(MergeRangeSegmentItemViewModel.IsSelected))
                {
                    OnPropertyChanged(nameof(MergeRangeSelectedCountLabel));
                    OnPropertyChanged(nameof(HasSelectedMergeRangeSegments));
                    OnPropertyChanged(nameof(CanApplyMergeRangeSelection));
                }
            };
            _allMergeRangeSegments.Add(item);
        }

        if (_allMergeRangeSegments.Count > 0)
        {
            AddMergeRangeBlock(UnicodeCoverageBlocks.AllBlocks, 0, 0);
            foreach (var block in UnicodeCoverageBlocks.KnownBlocks)
            {
                AddMergeRangeBlock(block.Name, block.Start, block.End);
            }

            AddMergeRangeBlock(UnicodeCoverageBlocks.OtherCoverage, 0, 0, isOther: true);
        }

        SelectedMergeRangeBlock = MergeRangeBlocks.FirstOrDefault();
        NotifyMergeRangeDialogState();
    }

    private void AddMergeRangeBlock(string name, int start, int end, bool isOther = false)
    {
        var segments = _allMergeRangeSegments.Where(segment =>
            string.Equals(name, UnicodeCoverageBlocks.AllBlocks, StringComparison.Ordinal)
            || string.Equals(segment.BlockName, name, StringComparison.Ordinal)).ToList();
        if (segments.Count == 0)
        {
            return;
        }

        var baseCount = CountByPresence(segments, GlyphCoveragePresence.BaseOnly)
                        + CountByPresence(segments, GlyphCoveragePresence.Both);
        var supplementalCount = CountByPresence(segments, GlyphCoveragePresence.SupplementalOnly)
                                + CountByPresence(segments, GlyphCoveragePresence.Both);
        var sharedCount = CountByPresence(segments, GlyphCoveragePresence.Both);
        MergeRangeBlocks.Add(new MergeRangeBlockItemViewModel(name, start, end, baseCount, supplementalCount, sharedCount, isOther));
    }

    private static int CountByPresence(IReadOnlyList<MergeRangeSegmentItemViewModel> segments, GlyphCoveragePresence presence) =>
        segments
            .Where(segment => segment.Segment.Presence == presence)
            .Sum(segment => segment.Range.Count);

    private void RefreshVisibleMergeRangeSegments()
    {
        OnPropertyChanged(nameof(MergeRangeSegments));
        NotifyMergeRangeDialogState();
    }

    private IEnumerable<MergeRangeSegmentItemViewModel> GetVisibleMergeRangeSegments()
    {
        if (SelectedMergeRangeBlock is null)
        {
            return [];
        }

        return _allMergeRangeSegments.Where(segment =>
            SelectedMergeRangeBlock.IsAll
            || string.Equals(segment.BlockName, SelectedMergeRangeBlock.Name, StringComparison.Ordinal));
    }

    private static string BuildCoverageSummary(string familyName, string styleName, GlyphCoverage coverage)
    {
        if (!coverage.HasCoverage)
        {
            return $"{familyName} · {styleName} · {coverage.EmptyMessage}";
        }

        return AppText.CurrentCultureCode == AppText.EnglishCultureCode
            ? $"{familyName} · {styleName} · {coverage.TotalCodePointCount:N0} Unicode-mapped code points · {coverage.Ranges.Count:N0} continuous segments"
            : $"{familyName} · {styleName} · {coverage.TotalCodePointCount:N0} 个 Unicode 映射码位 · {coverage.Ranges.Count:N0} 个连续段";
    }

    private static string BuildPendingMergeRangeSummary(FontFamilyItemViewModel? font, FontFaceItemViewModel? face, string fallbackLabel)
    {
        if (font is null)
        {
            return AppText.CurrentCultureCode == AppText.EnglishCultureCode
                ? $"{fallbackLabel} is not selected."
                : $"未选择{fallbackLabel}。";
        }

        return face is null
            ? AppText.CurrentCultureCode == AppText.EnglishCultureCode
                ? $"{font.FamilyName} · No readable styles"
                : $"{font.FamilyName} · 没有可读取的样式"
            : AppText.CurrentCultureCode == AppText.EnglishCultureCode
                ? $"{font.FamilyName} · {face.StyleLabel} · Waiting to read"
                : $"{font.FamilyName} · {face.StyleLabel} · 等待读取";
    }

    private static IReadOnlyList<GlyphCoverageSegment> BuildComparisonSegments(
        IReadOnlyList<DomainUnicodeRange> baseRanges,
        IReadOnlyList<DomainUnicodeRange> supplementalRanges)
    {
        if (baseRanges.Count == 0 && supplementalRanges.Count == 0)
        {
            return [];
        }

        var boundaries = new SortedSet<int>();
        AddRangeBoundaries(boundaries, baseRanges);
        AddRangeBoundaries(boundaries, supplementalRanges);
        foreach (var block in UnicodeCoverageBlocks.KnownBlocks)
        {
            AddBoundary(boundaries, block.Start);
            AddBoundary(boundaries, block.End + 1);
        }

        AddBoundary(boundaries, 0x110000);
        var ordered = boundaries.ToList();
        var result = new List<GlyphCoverageSegment>();
        var baseIndex = 0;
        var supplementalIndex = 0;
        for (var index = 0; index < ordered.Count - 1; index++)
        {
            var start = ordered[index];
            var end = Math.Min(0x10FFFF, ordered[index + 1] - 1);
            if (start > end || start > 0x10FFFF)
            {
                continue;
            }

            while (baseIndex < baseRanges.Count && baseRanges[baseIndex].End < start)
            {
                baseIndex++;
            }

            while (supplementalIndex < supplementalRanges.Count && supplementalRanges[supplementalIndex].End < start)
            {
                supplementalIndex++;
            }

            var hasBase = baseIndex < baseRanges.Count
                          && baseRanges[baseIndex].Start <= start
                          && baseRanges[baseIndex].End >= end;
            var hasSupplemental = supplementalIndex < supplementalRanges.Count
                                  && supplementalRanges[supplementalIndex].Start <= start
                                  && supplementalRanges[supplementalIndex].End >= end;
            if (!hasBase && !hasSupplemental)
            {
                continue;
            }

            var presence = (hasBase, hasSupplemental) switch
            {
                (true, true) => GlyphCoveragePresence.Both,
                (true, false) => GlyphCoveragePresence.BaseOnly,
                _ => GlyphCoveragePresence.SupplementalOnly
            };
            var blockName = UnicodeCoverageBlocks.FindKnownBlock(start)?.Name ?? UnicodeCoverageBlocks.OtherCoverage;
            result.Add(new GlyphCoverageSegment(new DomainUnicodeRange(start, end), blockName, presence));
        }

        return result;
    }

    private static void AddRangeBoundaries(SortedSet<int> boundaries, IReadOnlyList<DomainUnicodeRange> ranges)
    {
        foreach (var range in ranges)
        {
            AddBoundary(boundaries, range.Start);
            AddBoundary(boundaries, range.End + 1);
        }
    }

    private static void AddBoundary(SortedSet<int> boundaries, int value)
    {
        if (value is >= 0 and <= 0x110000)
        {
            boundaries.Add(value);
        }
    }

    private static IReadOnlyList<DomainUnicodeRange> NormalizeRanges(IReadOnlyList<DomainUnicodeRange> ranges)
    {
        var ordered = ranges
            .OrderBy(range => range.Start)
            .ThenBy(range => range.End)
            .ToList();
        var normalized = new List<DomainUnicodeRange>();
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

    private void NotifyMergeRangeDialogState()
    {
        OnPropertyChanged(nameof(MergeRangeSegments));
        OnPropertyChanged(nameof(HasMergeRangeBlocks));
        OnPropertyChanged(nameof(HasMergeRangeSegments));
        OnPropertyChanged(nameof(HasLoadedMergeRangeSegments));
        OnPropertyChanged(nameof(IsMergeRangeEmptyVisible));
        OnPropertyChanged(nameof(IsMergeRangeBlockEmptyVisible));
        OnPropertyChanged(nameof(HasSelectedMergeRangeSegments));
        OnPropertyChanged(nameof(CanApplyMergeRangeSelection));
        OnPropertyChanged(nameof(MergeRangeSelectedCountLabel));
    }

    private void ReplaceMergeFontList(
        ObservableCollection<FontFamilyItemViewModel> target,
        string searchText,
        string? selectedFamilyName,
        Action<FontFamilyItemViewModel?> select)
    {
        target.Clear();
        foreach (var font in _allFonts.Where(font => MatchesMergeSearch(font, searchText)))
        {
            target.Add(font);
        }

        select(target.FirstOrDefault(font => string.Equals(font.FamilyName, selectedFamilyName, StringComparison.CurrentCultureIgnoreCase))
               ?? target.FirstOrDefault());
    }

    private static bool MatchesMergeSearch(FontFamilyItemViewModel font, string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        return font.FamilyName.Contains(searchText, StringComparison.CurrentCultureIgnoreCase)
               || font.FormatSummary.Contains(searchText, StringComparison.CurrentCultureIgnoreCase)
               || font.Faces.Any(face => face.FullName.Contains(searchText, StringComparison.CurrentCultureIgnoreCase));
    }

    private FontMergeRequest CreateMergeRequest(bool includeOutput)
    {
        return new FontMergeRequest(
            CreateMergeSelection(SelectedMergeBaseFont),
            CreateMergeSelection(SelectedMergeSupplementalFont),
            MergeUnicodeRanges,
            includeOutput ? MergeOutputPath : "",
            MergeOutputFontName,
            MergeLicenseConfirmed,
            MergeMode: SelectedMergeMode);
    }

    private static FontMergeFontSelection? CreateMergeSelection(FontFamilyItemViewModel? font)
    {
        if (font is null)
        {
            return null;
        }

        var record = font.ToRecord();
        var face = SelectDefaultPreviewFace(font)?.ToRecord() ?? record.Faces.FirstOrDefault();
        if (face is null)
        {
            return null;
        }

        return new FontMergeFontSelection(
            record.FamilyName,
            FontStyleVariantFormatter.FormatFaceStyle(face.SubfamilyName, face.Weight, face.Slant),
            new FontFileRef(face.File.Path, face.File.Format, face.File.Sha256),
            new LicenseSnapshot(record.LicenseStatus, record.LicenseText));
    }

    private void ApplyMergePreview(FontMergePreview preview)
    {
        _currentMergePreview = preview;
        MergeConflicts.Clear();
        foreach (var conflict in preview.Conflicts)
        {
            MergeConflicts.Add(new MergeConflictItemViewModel(conflict));
        }

        MergeIssues.Clear();
        foreach (var issue in preview.Issues)
        {
            MergeIssues.Add(new MergeIssueItemViewModel(issue));
        }

        NotifyMergePreviewState();
    }

    private void ApplyMergeReport(FontMergeReport report, string reportPath)
    {
        SelectedMergeModeLabel = FormatMergeMode(report.MergeMode);
        MergeReportTitle = report.Succeeded ? L("合并结果：成功") : L("合并结果：失败");
        MergeReportBadge = report.Succeeded ? L("成功") : L("失败");
        MergeReportOutputPath = string.IsNullOrWhiteSpace(report.OutputPath) ? L("未生成输出字体") : report.OutputPath;
        MergeReportInputFonts = $"{report.BaseFontFamily} + {report.SupplementalFontFamily}";
        MergeReportRangeLabel = report.Ranges.Count == 0 ? L("未记录") : string.Join(", ", report.Ranges);
        MergeReportStatsLabel = FormatMergeReportStats(report);
        MergeReportLicenseTime = report.LicenseConfirmedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? L("未确认");
        MergeReportErrorMessage = report.ErrorMessage;
        MergeReportPath = reportPath;
        MergeIssues.Clear();
        foreach (var issue in report.Issues)
        {
            MergeIssues.Add(new MergeIssueItemViewModel(issue));
        }

        NotifyMergeReportState();
        NotifyMergePreviewState();
    }

    private void RefreshMergeStepState()
    {
        foreach (var step in MergeSteps)
        {
            step.IsActive = step.Index == MergeStepIndex;
        }
    }

    private void NotifyMergePreviewState()
    {
        OnPropertyChanged(nameof(HasMergePreview));
        OnPropertyChanged(nameof(HasMergeConflicts));
        OnPropertyChanged(nameof(HasMergeIssues));
        OnPropertyChanged(nameof(MergePreviewSummary));
        OnPropertyChanged(nameof(MergeDefaultStrategy));
    }

    private void NotifyMergeExportState()
    {
        OnPropertyChanged(nameof(HasSelectedMergeBaseFont));
        OnPropertyChanged(nameof(HasSelectedMergeSupplementalFont));
    }

    private void NotifyMergeReportState()
    {
        OnPropertyChanged(nameof(HasMergeReport));
    }

    private void UpdateDefaultMergeOutputName()
    {
        if (!string.IsNullOrWhiteSpace(MergeOutputFontName) || SelectedMergeBaseFont is null)
        {
            return;
        }

        MergeOutputFontName = $"{SelectedMergeBaseFont.FamilyName} Patch";
    }

    private string BuildSuggestedMergeOutputFileName()
    {
        var name = string.IsNullOrWhiteSpace(MergeOutputFontName)
            ? "GlyphStash-Merged"
            : MergeOutputFontName;
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '-');
        }

        return $"{name}.ttf";
    }

    private static GlyphStash.Domain.Fonts.UnicodeRange ParseReportRange(string label)
    {
        try
        {
            return UnicodeRangeParser.Parse(label).FirstOrDefault() ?? new GlyphStash.Domain.Fonts.UnicodeRange(0, 0);
        }
        catch
        {
            return new GlyphStash.Domain.Fonts.UnicodeRange(0, 0);
        }
    }

    private static string BuildMergeModeStrategy(FontMergeMode mergeMode) =>
        mergeMode == FontMergeMode.Overwrite
            ? L("覆盖模式：指定范围内补充字体存在的码位覆盖基础字体")
            : L("补全模式：基础字体已有码位默认跳过");

    private static string FormatMergeMode(FontMergeMode mergeMode) =>
        mergeMode == FontMergeMode.Overwrite ? OverwriteMergeModeLabel : SupplementMergeModeLabel;

    private static string FormatMergeModeLabel(FontMergeMode mergeMode) =>
        mergeMode == FontMergeMode.Overwrite
            ? (AppText.CurrentCultureCode == AppText.EnglishCultureCode ? "Overwrite" : OverwriteMergeModeLabel)
            : (AppText.CurrentCultureCode == AppText.EnglishCultureCode ? "Supplement (append)" : SupplementMergeModeLabel);

    private static string FormatMergeReportStats(FontMergeReport report) =>
        AppText.CurrentCultureCode == AppText.EnglishCultureCode
            ? $"Mode {FormatMergeModeLabel(report.MergeMode)} · Checked {report.RequestedCodePointCount:N0} · Merged {report.MergedCodePointCount:N0} · Skipped {report.SkippedDuplicateCodePointCount:N0} · Overwritten {report.OverwrittenCodePointCount:N0} · Missing {report.MissingCodePointCount:N0} · Conflicts {report.ConflictCount:N0}"
            : $"模式 {FormatMergeModeLabel(report.MergeMode)} · 检查 {report.RequestedCodePointCount:N0} · 合并 {report.MergedCodePointCount:N0} · 跳过 {report.SkippedDuplicateCodePointCount:N0} · 覆盖 {report.OverwrittenCodePointCount:N0} · 缺失 {report.MissingCodePointCount:N0} · 冲突 {report.ConflictCount:N0}";

    private void RebuildManagementOptions()
    {
        TagOptions.Clear();
        CollectionOptions.Clear();

        var selectedTags = SelectedFont?.Tags.ToHashSet(StringComparer.CurrentCultureIgnoreCase) ?? [];
        var selectedCollections = SelectedFont?.Collections.ToHashSet(StringComparer.CurrentCultureIgnoreCase) ?? [];

        foreach (var tag in AvailableTags
                     .Select(tag => new NameOptionViewModel(tag.Name, tag.FontCount, selectedTags.Contains(tag.Name)))
                     .Concat(selectedTags
                         .Where(tag => AvailableTags.All(available => !string.Equals(available.Name, tag, StringComparison.CurrentCultureIgnoreCase)))
                         .Select(tag => new NameOptionViewModel(tag, 0, true)))
                     .OrderBy(option => option.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            TagOptions.Add(tag);
        }

        foreach (var collection in AvailableCollections
                     .Select(collection => new NameOptionViewModel(collection, 0, selectedCollections.Contains(collection)))
                     .Concat(selectedCollections
                         .Where(collection => AvailableCollections.All(available => !string.Equals(available, collection, StringComparison.CurrentCultureIgnoreCase)))
                         .Select(collection => new NameOptionViewModel(collection, 0, true)))
                     .OrderBy(option => option.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            CollectionOptions.Add(collection);
        }
    }

    private void NotifyTrayHideState()
    {
        OnPropertyChanged(nameof(CanHideToTray));
        OnPropertyChanged(nameof(TrayHideBlockReason));
    }

    private void NotifyCollectionState()
    {
        OnPropertyChanged(nameof(HasFonts));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasSelectedFont));
        OnPropertyChanged(nameof(HasNoSelectedFont));
        OnPropertyChanged(nameof(HasSelectedCollection));
        OnPropertyChanged(nameof(FontCountLabel));
        OnPropertyChanged(nameof(EmptyStateMessage));
    }

    private CancellationToken ResolveCancellationToken(CancellationToken cancellationToken) =>
        cancellationToken.CanBeCanceled ? cancellationToken : LifetimeToken;

    private void RunLatestAsync(
        ref CancellationTokenSource? slot,
        Func<CancellationToken, Task> operation,
        Action<Exception>? onError = null)
    {
        if (IsDisposed)
        {
            return;
        }

        CancelAndDispose(ref slot);
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(LifetimeToken);
        slot = cancellation;
        _ = RunFireAndForgetAsync(cancellation, operation, onError);
    }

    private CancellationToken BeginLatest(ref CancellationTokenSource? slot)
    {
        CancelAndDispose(ref slot);
        slot = CancellationTokenSource.CreateLinkedTokenSource(LifetimeToken);
        return slot.Token;
    }

    private async Task RunFireAndForgetAsync(
        CancellationTokenSource cancellation,
        Func<CancellationToken, Task> operation,
        Action<Exception>? onError)
    {
        try
        {
            await operation(cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (onError is not null)
            {
                onError(ex);
            }
            else
            {
                ShowToast(ex.ToUserMessage());
            }
        }
    }

    private static void CancelAndDispose(ref CancellationTokenSource? cancellation)
    {
        if (cancellation is null)
        {
            return;
        }

        cancellation.Cancel();
        cancellation.Dispose();
        cancellation = null;
    }

    private void NotifyNavigationState()
    {
        OnPropertyChanged(nameof(IsFontLibraryPage));
        OnPropertyChanged(nameof(IsCollectionsPage));
        OnPropertyChanged(nameof(IsOnlineFontsPage));
        OnPropertyChanged(nameof(IsMergeToolPage));
        OnPropertyChanged(nameof(IsSettingsPage));
        OnPropertyChanged(nameof(IsPlaceholderPage));
        OnPropertyChanged(nameof(IsGlyphBrowserPage));
    }

    private void UpdateCurrentPage()
    {
        CurrentPage = true switch
        {
            _ when IsGlyphBrowserOpen => _glyphBrowserPage,
            _ when SelectedNavigationItem?.IsFontLibrary == true => _fontLibraryPage,
            _ when SelectedNavigationItem?.IsCollections == true => _collectionsPage,
            _ when SelectedNavigationItem?.Key == "online-fonts" => _onlineFontsPage,
            _ when SelectedNavigationItem?.Key == "merge-tool" => _mergeToolPage,
            _ when SelectedNavigationItem?.IsSettings == true => _settingsPage,
            _ => _placeholderPage
        };
    }

    private static IReadOnlyList<string> ParseNames(string text) =>
        text.Split([',', ';', '，', '；', '\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    private static IReadOnlyList<string> MergeNames(IEnumerable<string> first, IEnumerable<string> second) =>
        first.Concat(second)
            .Select(name => name.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    private static string NormalizeFilter(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

    private void UpdateSelectedFaceFlags(FontFaceItemViewModel? selectedFace)
    {
        foreach (var face in _allFonts.SelectMany(font => font.Faces).Concat(SelectedFont?.Faces ?? []))
        {
            face.IsSelected = ReferenceEquals(face, selectedFace);
        }
    }

    private static FontWeight ToFontWeight(int weight) => weight switch
    {
        <= 100 => FontWeight.Thin,
        200 => FontWeight.ExtraLight,
        300 => FontWeight.Light,
        400 => FontWeight.Normal,
        500 => FontWeight.Medium,
        600 => FontWeight.SemiBold,
        700 => FontWeight.Bold,
        800 => FontWeight.ExtraBold,
        >= 900 => FontWeight.Black,
        _ => (FontWeight)weight
    };

    private static FontFaceItemViewModel? SelectDefaultPreviewFace(FontFamilyItemViewModel? font)
    {
        if (font is null)
        {
            return null;
        }

        return font.Faces.FirstOrDefault(face =>
                face.Weight == 400 && !string.Equals(face.Slant, "Italic", StringComparison.OrdinalIgnoreCase))
            ?? font.Faces.FirstOrDefault(face => face.StyleLabel.Contains("Regular 400", StringComparison.OrdinalIgnoreCase))
            ?? font.Faces.FirstOrDefault();
    }

    private static FontFaceItemViewModel? SelectMergeFace(FontFamilyItemViewModel? font) => SelectDefaultPreviewFace(font);

    private static string NormalizeOnlineFilter(string value, string allValue) =>
        string.Equals(value, allValue, StringComparison.CurrentCultureIgnoreCase) ? "" : value;

    private IReadOnlyList<string> BuildOnlineCapabilities()
    {
        var capabilities = new List<string>();
        if (OnlineCapabilityVf)
        {
            capabilities.Add("VF");
        }

        if (OnlineCapabilityWoff2)
        {
            capabilities.Add("WOFF2");
        }

        return capabilities;
    }

    private static void ReplaceFilterOptions(ObservableCollection<string> target, IReadOnlyList<string> values)
    {
        for (var index = 0; index < values.Count; index++)
        {
            var value = values[index];
            var existingIndex = IndexOfFilterOption(target, value);
            if (existingIndex < 0)
            {
                target.Insert(index, value);
                continue;
            }

            if (existingIndex != index)
            {
                target.Move(existingIndex, index);
            }

            if (!string.Equals(target[index], value, StringComparison.Ordinal))
            {
                target[index] = value;
            }
        }

        while (target.Count > values.Count)
        {
            target.RemoveAt(target.Count - 1);
        }
    }

    private static int IndexOfFilterOption(IReadOnlyList<string> options, string value)
    {
        for (var index = 0; index < options.Count; index++)
        {
            if (string.Equals(options[index], value, StringComparison.CurrentCultureIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private void SelectTagFilter(string preferredFilter)
    {
        var filter = TagFilters.FirstOrDefault(candidate => string.Equals(candidate, preferredFilter, StringComparison.CurrentCultureIgnoreCase)) ?? AllTagFilter;
        if (SelectedTagFilter == filter)
        {
            OnPropertyChanged(nameof(SelectedTagFilter));
            return;
        }

        SelectedTagFilter = filter;
    }

    private void SelectCollectionFilter(string preferredFilter)
    {
        var filter = CollectionFilters.FirstOrDefault(candidate => string.Equals(candidate, preferredFilter, StringComparison.CurrentCultureIgnoreCase)) ?? AllCollectionFilter;
        if (SelectedCollectionFilter == filter)
        {
            OnPropertyChanged(nameof(SelectedCollectionFilter));
            return;
        }

        SelectedCollectionFilter = filter;
    }

    private async void ShowToast(string message)
    {
        ToastMessage = message;
        IsToastVisible = true;
        await Task.Delay(2600);
        if (ToastMessage == message)
        {
            IsToastVisible = false;
        }
    }
}
