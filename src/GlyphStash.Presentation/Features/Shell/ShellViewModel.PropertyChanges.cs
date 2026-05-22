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
        ApplyFilters();
    }

    partial void OnSelectedStateFilterChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnSelectedTagFilterChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            SelectTagFilter(AllTagFilter);
            return;
        }

        ApplyFilters();
    }

    partial void OnSelectedCollectionFilterChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            SelectCollectionFilter(AllCollectionFilter);
            return;
        }

        ApplyFilters();
    }

    partial void OnCollectionSearchTextChanged(string value) => ApplyCollectionFilter();

    partial void OnPendingDeleteTagNameChanged(string value) => OnPropertyChanged(nameof(PendingDeleteTagConfirmationText));

    partial void OnSelectedCollectionChanged(CollectionItemViewModel? value)
    {
        RefreshCollectionFonts();
        OnPropertyChanged(nameof(HasSelectedCollection));
    }

    private void OnSelectedLanguageChanged(LanguageOptionViewModel? value)
    {
        if (value is null)
        {
            return;
        }

        _localizationService.SetCulture(value.CultureCode);
        RunLatestAsync(
            ref _languageSaveCancellation,
            cancellationToken => SaveSelectedLanguageAsync(value, cancellationToken),
            ex => StatusMessage = ex.ToUserMessage());
    }

    partial void OnSelectedFontChanged(FontFamilyItemViewModel? value)
    {
        SelectedPreviewFace = SelectDefaultPreviewFace(value);
    }

    partial void OnSelectedPreviewFaceChanged(FontFaceItemViewModel? value)
    {
        UpdateSelectedFaceFlags(value);
    }

    partial void OnImportInstallForCurrentUserChanged(bool value)
    {
        if (value)
        {
            ImportTemporarilyActivate = false;
        }

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

    partial void OnMergeStepIndexChanged(int value) => RefreshMergeStepState();

    partial void OnMergeBaseSearchTextChanged(string value) => RefreshMergeFontLists();

    partial void OnMergeSupplementalSearchTextChanged(string value) => RefreshMergeFontLists();

    partial void OnSelectedMergeBaseFontChanged(FontFamilyItemViewModel? value)
    {
        UpdateDefaultMergeOutputName();
    }

    partial void OnSelectedMergeSupplementalFontChanged(FontFamilyItemViewModel? value)
    {
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
        _currentMergePreview = null;
        MergeConflicts.Clear();
        MergeIssues.Clear();
        NotifyMergePreviewState();
    }

    partial void OnMergeOutputFontNameChanged(string value) => NotifyMergeExportState();

    partial void OnMergeOutputPathChanged(string value) => NotifyMergeExportState();

    partial void OnMergeLicenseConfirmedChanged(bool value) => NotifyMergeExportState();

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
