using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using GlyphStash.Domain.Fonts;
using GlyphStash.Localization;
using DomainUnicodeRange = GlyphStash.Domain.Fonts.UnicodeRange;

namespace GlyphStash.Presentation.ViewModels;

public sealed partial class ShellViewModel
{
    partial void OnSearchTextChanged(string value) => ApplyFilters();

    partial void OnSelectedSourceFilterChanged(string value)
    {
        if (!_isSyncingLocalizedOptions)
        {
            SelectSourceFilterOption(value);
        }

        ApplyFilters();
    }

    partial void OnSelectedSourceFilterOptionChanged(LocalizedOptionViewModel? value)
    {
        if (_isSyncingLocalizedOptions)
        {
            return;
        }

        SetStableFilterFromOption(value, fallback: AllSourceFilter, selected => SelectedSourceFilter = selected);
        ApplyFilters();
    }

    partial void OnSelectedStateFilterChanged(string value)
    {
        if (!_isSyncingLocalizedOptions)
        {
            SelectStateFilterOption(value);
        }

        ApplyFilters();
    }

    partial void OnSelectedStateFilterOptionChanged(LocalizedOptionViewModel? value)
    {
        if (_isSyncingLocalizedOptions)
        {
            return;
        }

        SetStableFilterFromOption(value, fallback: AllStateFilter, selected => SelectedStateFilter = selected);
        ApplyFilters();
    }

    partial void OnSelectedTagFilterChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            SelectTagFilter(AllTagFilter);
            return;
        }

        if (!_isSyncingLocalizedOptions)
        {
            SelectTagFilterOption(value);
        }

        ApplyFilters();
    }

    partial void OnSelectedTagFilterOptionChanged(LocalizedOptionViewModel? value)
    {
        if (_isSyncingLocalizedOptions)
        {
            return;
        }

        SetStableFilterFromOption(value, fallback: AllTagFilter, selected => SelectedTagFilter = selected);
        ApplyFilters();
    }

    partial void OnSelectedCollectionFilterChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            SelectCollectionFilter(AllCollectionFilter);
            return;
        }

        if (!_isSyncingLocalizedOptions)
        {
            SelectCollectionFilterOption(value);
        }

        ApplyFilters();
    }

    partial void OnSelectedCollectionFilterOptionChanged(LocalizedOptionViewModel? value)
    {
        if (_isSyncingLocalizedOptions)
        {
            return;
        }

        SetStableFilterFromOption(value, fallback: AllCollectionFilter, selected => SelectedCollectionFilter = selected);
        ApplyFilters();
    }

    partial void OnSelectedOnlineSubsetChanged(string value)
    {
        if (!_isSyncingLocalizedOptions)
        {
            SelectOnlineSubsetOption(value);
        }
    }

    partial void OnSelectedOnlineSubsetOptionChanged(LocalizedOptionViewModel? value)
    {
        if (_isSyncingLocalizedOptions)
        {
            return;
        }

        SetStableFilterFromOption(value, fallback: AllOnlineSubset, selected => SelectedOnlineSubset = selected);
    }

    partial void OnSelectedOnlineCategoryChanged(string value)
    {
        if (!_isSyncingLocalizedOptions)
        {
            SelectOnlineCategoryOption(value);
        }
    }

    partial void OnSelectedOnlineCategoryOptionChanged(LocalizedOptionViewModel? value)
    {
        if (_isSyncingLocalizedOptions)
        {
            return;
        }

        SetStableFilterFromOption(value, fallback: AllOnlineCategory, selected => SelectedOnlineCategory = selected);
    }

    partial void OnCollectionSearchTextChanged(string value) => ApplyCollectionFilter();

    partial void OnPendingDeleteTagNameChanged(string value) => OnPropertyChanged(nameof(PendingDeleteTagConfirmationText));

    partial void OnSelectedCollectionChanged(CollectionItemViewModel? value)
    {
        RefreshCollectionFonts();
        OnPropertyChanged(nameof(HasSelectedCollection));
    }

    partial void OnManagedFontDirectoryChanged(string value)
    {
        OnPropertyChanged(nameof(HasManagedFontDirectory));
        OnPropertyChanged(nameof(ManagedDirectoryStatus));
    }

    partial void OnGoogleFontsApiKeyTextChanged(string value) => OnPropertyChanged(nameof(GoogleFontsApiKeyStatus));

    partial void OnSelectedLanguageChanged(LanguageOptionViewModel? value)
    {
        if (value is null)
        {
            return;
        }

        _localizationService.SetCulture(value.CultureCode);
        if (_isApplyingSettings)
        {
            return;
        }

        RunLatestAsync(
            ref _languageSaveCancellation,
            cancellationToken => SaveSelectedLanguageAsync(value, cancellationToken),
            ex => StatusMessage = ex.ToUserMessage());
    }

    partial void OnSelectedFontChanged(FontFamilyItemViewModel? value)
    {
        SelectedPreviewFace = SelectDefaultPreviewFace(value);
        OnPropertyChanged(nameof(HasSelectedFont));
        OnPropertyChanged(nameof(HasNoSelectedFont));
    }

    partial void OnSelectedPreviewFaceChanged(FontFaceItemViewModel? value)
    {
        UpdateSelectedFaceFlags(value);
        OnPropertyChanged(nameof(HasSelectedPreviewFace));
        OnPropertyChanged(nameof(SelectedPreviewFontFamily));
        OnPropertyChanged(nameof(SelectedPreviewFontWeight));
        OnPropertyChanged(nameof(SelectedPreviewFontStyle));
    }

    partial void OnImportInstallForCurrentUserChanged(bool value)
    {
        if (value)
        {
            ImportTemporarilyActivate = false;
        }

        OnPropertyChanged(nameof(CanImportTemporarilyActivate));
        OnPropertyChanged(nameof(ImportTemporaryActivationReason));
    }

    partial void OnImportTemporarilyActivateChanged(bool value)
    {
        if (value && ImportInstallForCurrentUser)
        {
            ImportTemporarilyActivate = false;
        }
    }

    partial void OnSelectedNavigationItemChanged(NavigationItemViewModel? value)
    {
        if (value is not null)
        {
            StatusMessage = value.IsImplemented
                ? FormatCurrentPageStatus(value)
                : FormatPlaceholderPageStatus(value);
        }

        NotifyNavigationState();
        UpdateCurrentPage();
        OnPropertyChanged(nameof(CurrentPageTitle));
        OnPropertyChanged(nameof(CurrentPageDescription));
        OnPropertyChanged(nameof(CurrentPageMilestone));
    }

    partial void OnIsGlyphBrowserOpenChanged(bool value)
    {
        NotifyNavigationState();
        UpdateCurrentPage();
    }

    partial void OnSelectedRemoteFontChanged(RemoteFontFamilyItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelectedRemoteFont));
    }

    partial void OnSelectedGlyphChanged(GlyphItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelectedGlyph));
    }

    partial void OnGlyphSearchTextChanged(string value)
    {
        if (IsGlyphBrowserOpen)
        {
            GlyphPageNumber = 1;
            RunLatestAsync(
                ref _glyphLoadCancellation,
                LoadGlyphsAsync,
                ex => GlyphStatus = ex.ToUserMessage());
        }
    }

    partial void OnSelectedUnicodeBlockChanged(string value)
    {
        if (IsGlyphBrowserOpen)
        {
            GlyphPageNumber = 1;
            RunLatestAsync(
                ref _glyphLoadCancellation,
                LoadGlyphsAsync,
                ex => GlyphStatus = ex.ToUserMessage());
        }
    }

    partial void OnGlyphPageNumberChanged(int value) => OnPropertyChanged(nameof(GlyphPageLabel));

    partial void OnGlyphTotalPagesChanged(int value) => OnPropertyChanged(nameof(GlyphPageLabel));

    partial void OnMergeStepIndexChanged(int value) => RefreshMergeStepState();

    partial void OnMergeBaseSearchTextChanged(string value) => RefreshMergeFontLists();

    partial void OnMergeSupplementalSearchTextChanged(string value) => RefreshMergeFontLists();

    partial void OnSelectedMergeBaseFontChanged(FontFamilyItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelectedMergeBaseFont));
        UpdateDefaultMergeOutputName();
    }

    partial void OnSelectedMergeSupplementalFontChanged(FontFamilyItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelectedMergeSupplementalFont));
        UpdateDefaultMergeOutputName();
    }

    partial void OnMergeUnicodeRangesChanged(string value)
    {
        _currentMergePreview = null;
        MergeConflicts.Clear();
        MergeIssues.Clear();
        NotifyMergePreviewState();
    }

    partial void OnSelectedMergeRangeBlockChanged(MergeRangeBlockItemViewModel? value)
    {
        RefreshVisibleMergeRangeSegments();
    }

    partial void OnSelectedMergeModeLabelChanged(string value)
    {
        if (!_isSyncingLocalizedOptions)
        {
            SelectMergeModeOption(value);
        }

        _currentMergePreview = null;
        MergeConflicts.Clear();
        MergeIssues.Clear();
        OnPropertyChanged(nameof(SelectedMergeMode));
        OnPropertyChanged(nameof(MergeModeDescription));
        NotifyMergePreviewState();
    }

    partial void OnSelectedMergeModeOptionChanged(LocalizedOptionViewModel? value)
    {
        if (_isSyncingLocalizedOptions)
        {
            return;
        }

        SetStableFilterFromOption(value, fallback: SupplementMergeModeLabel, selected => SelectedMergeModeLabel = selected);
    }

    partial void OnMergeOutputFontNameChanged(string value) => NotifyMergeExportState();

    partial void OnMergeOutputPathChanged(string value) => NotifyMergeExportState();

    partial void OnMergeLicenseConfirmedChanged(bool value) => NotifyMergeExportState();

    partial void OnIsBusyChanged(bool value) => NotifyTrayHideState();

    partial void OnIsOnlineSearchBusyChanged(bool value) => NotifyTrayHideState();

    partial void OnIsMergeBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanGoPreviousMergeStep));
        OnPropertyChanged(nameof(CanCancelMerge));
        OnPropertyChanged(nameof(MergeProgressLabel));
        NotifyTrayHideState();
    }

    partial void OnIsMergeRangeDialogBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsMergeRangeEmptyVisible));
        OnPropertyChanged(nameof(IsMergeRangeBlockEmptyVisible));
        OnPropertyChanged(nameof(CanApplyMergeRangeSelection));
        NotifyTrayHideState();
    }

    partial void OnIsGlyphLoadingChanged(bool value) => NotifyTrayHideState();

    partial void OnMergeStatusChanged(string value) => OnPropertyChanged(nameof(MergeProgressLabel));

    partial void OnMergeProgressPercentChanged(int value) => OnPropertyChanged(nameof(MergeProgressLabel));

    partial void OnMergeProgressStageChanged(string value) => OnPropertyChanged(nameof(MergeProgressLabel));

    partial void OnMergeProgressMessageChanged(string value) => OnPropertyChanged(nameof(MergeProgressLabel));

    partial void OnPreviewFontSizeChanged(double value)
    {
        foreach (var font in _allFonts)
        {
            font.SetPreview(PreviewText, PreviewFontSize);
        }

        OnPropertyChanged(nameof(PreviewFontSizeLabel));
    }

    partial void OnPreviewTextChanged(string value)
    {
        foreach (var font in _allFonts)
        {
            font.SetPreview(PreviewText, PreviewFontSize);
        }
    }

}
