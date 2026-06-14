using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using GlyphStash.Presentation.ViewModels;

namespace GlyphStash.Presentation.Features.Shell;

public abstract class ShellFeatureViewModel : ObservableObject
{
    private ShellViewModel? _shell;

    internal void Attach(ShellViewModel shell)
    {
        if (_shell is not null)
        {
            _shell.PropertyChanged -= OnShellPropertyChanged;
        }

        _shell = shell;
        _shell.PropertyChanged += OnShellPropertyChanged;
    }

    protected ShellViewModel Shell => _shell ?? throw new InvalidOperationException("Feature view model has not been attached to the shell.");

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs args) =>
        OnPropertyChanged(args.PropertyName);

    public string SearchText { get => Shell.SearchText; set => Shell.SearchText = value; }
    public string SelectedSourceFilter { get => Shell.SelectedSourceFilter; set => Shell.SelectedSourceFilter = value; }
    public LocalizedOptionViewModel? SelectedSourceFilterOption { get => Shell.SelectedSourceFilterOption; set => Shell.SelectedSourceFilterOption = value; }
    public string SelectedStateFilter { get => Shell.SelectedStateFilter; set => Shell.SelectedStateFilter = value; }
    public LocalizedOptionViewModel? SelectedStateFilterOption { get => Shell.SelectedStateFilterOption; set => Shell.SelectedStateFilterOption = value; }
    public string SelectedTagFilter { get => Shell.SelectedTagFilter; set => Shell.SelectedTagFilter = value; }
    public LocalizedOptionViewModel? SelectedTagFilterOption { get => Shell.SelectedTagFilterOption; set => Shell.SelectedTagFilterOption = value; }
    public string SelectedCollectionFilter { get => Shell.SelectedCollectionFilter; set => Shell.SelectedCollectionFilter = value; }
    public LocalizedOptionViewModel? SelectedCollectionFilterOption { get => Shell.SelectedCollectionFilterOption; set => Shell.SelectedCollectionFilterOption = value; }
    public string PreviewText { get => Shell.PreviewText; set => Shell.PreviewText = value; }
    public double PreviewFontSize { get => Shell.PreviewFontSize; set => Shell.PreviewFontSize = value; }
    public FontFamilyItemViewModel? SelectedFont { get => Shell.SelectedFont; set => Shell.SelectedFont = value; }
    public FontFaceItemViewModel? SelectedPreviewFace { get => Shell.SelectedPreviewFace; set => Shell.SelectedPreviewFace = value; }
    public bool IsBusy { get => Shell.IsBusy; set => Shell.IsBusy = value; }
    public string ScanStatus => Shell.ScanStatus;
    public string StatusMessage => Shell.StatusMessage;
    public bool IsEmpty => Shell.IsEmpty;
    public bool HasFonts => Shell.HasFonts;
    public bool HasSelectedFont => Shell.HasSelectedFont;
    public bool HasNoSelectedFont => Shell.HasNoSelectedFont;
    public string EmptyStateMessage => Shell.EmptyStateMessage;
    public string PreviewFontSizeLabel => Shell.PreviewFontSizeLabel;
    public FontFamily SelectedPreviewFontFamily => Shell.SelectedPreviewFontFamily;
    public FontWeight SelectedPreviewFontWeight => Shell.SelectedPreviewFontWeight;
    public FontStyle SelectedPreviewFontStyle => Shell.SelectedPreviewFontStyle;
    public ObservableCollection<FontFamilyItemViewModel> Fonts => Shell.Fonts;
    public ObservableCollection<LocalizedOptionViewModel> SourceFilterOptions => Shell.SourceFilterOptions;
    public ObservableCollection<LocalizedOptionViewModel> StateFilterOptions => Shell.StateFilterOptions;
    public ObservableCollection<LocalizedOptionViewModel> TagFilterOptions => Shell.TagFilterOptions;
    public ObservableCollection<LocalizedOptionViewModel> CollectionFilterOptions => Shell.CollectionFilterOptions;

    public bool IsImportDialogOpen { get => Shell.IsImportDialogOpen; set => Shell.IsImportDialogOpen = value; }
    public string ImportStatus { get => Shell.ImportStatus; set => Shell.ImportStatus = value; }
    public string ManagedFontDirectory { get => Shell.ManagedFontDirectory; set => Shell.ManagedFontDirectory = value; }
    public string ManagedDirectoryStatus => Shell.ManagedDirectoryStatus;
    public ObservableCollection<ImportPreviewItemViewModel> ImportPreviewItems => Shell.ImportPreviewItems;
    public bool ImportInstallForCurrentUser { get => Shell.ImportInstallForCurrentUser; set => Shell.ImportInstallForCurrentUser = value; }
    public bool ImportTemporarilyActivate { get => Shell.ImportTemporarilyActivate; set => Shell.ImportTemporarilyActivate = value; }
    public bool CanImportTemporarilyActivate => Shell.CanImportTemporarilyActivate;
    public string ImportTemporaryActivationReason => Shell.ImportTemporaryActivationReason;
    public string ImportTagsText { get => Shell.ImportTagsText; set => Shell.ImportTagsText = value; }
    public string ImportCollectionsText { get => Shell.ImportCollectionsText; set => Shell.ImportCollectionsText = value; }
    public bool IsTagsDialogOpen { get => Shell.IsTagsDialogOpen; set => Shell.IsTagsDialogOpen = value; }
    public ObservableCollection<NameOptionViewModel> TagOptions => Shell.TagOptions;
    public ObservableCollection<NameOptionViewModel> CollectionOptions => Shell.CollectionOptions;
    public string TagEditorText { get => Shell.TagEditorText; set => Shell.TagEditorText = value; }
    public string CollectionEditorText { get => Shell.CollectionEditorText; set => Shell.CollectionEditorText = value; }
    public bool IsDeleteTagDialogOpen { get => Shell.IsDeleteTagDialogOpen; set => Shell.IsDeleteTagDialogOpen = value; }
    public string PendingDeleteTagConfirmationText => Shell.PendingDeleteTagConfirmationText;
    public bool IsUninstallDialogOpen { get => Shell.IsUninstallDialogOpen; set => Shell.IsUninstallDialogOpen = value; }

    public string NewCollectionName { get => Shell.NewCollectionName; set => Shell.NewCollectionName = value; }
    public string CollectionSearchText { get => Shell.CollectionSearchText; set => Shell.CollectionSearchText = value; }
    public ObservableCollection<CollectionItemViewModel> Collections => Shell.Collections;
    public CollectionItemViewModel? SelectedCollection { get => Shell.SelectedCollection; set => Shell.SelectedCollection = value; }
    public bool HasSelectedCollection => Shell.HasSelectedCollection;
    public ObservableCollection<FontFamilyItemViewModel> CollectionFonts => Shell.CollectionFonts;
    public bool IsDeleteCollectionDialogOpen { get => Shell.IsDeleteCollectionDialogOpen; set => Shell.IsDeleteCollectionDialogOpen = value; }

    public string OnlineSearchText { get => Shell.OnlineSearchText; set => Shell.OnlineSearchText = value; }
    public ObservableCollection<LocalizedOptionViewModel> OnlineSubsetOptionModels => Shell.OnlineSubsetOptionModels;
    public string SelectedOnlineSubset { get => Shell.SelectedOnlineSubset; set => Shell.SelectedOnlineSubset = value; }
    public LocalizedOptionViewModel? SelectedOnlineSubsetOption { get => Shell.SelectedOnlineSubsetOption; set => Shell.SelectedOnlineSubsetOption = value; }
    public ObservableCollection<LocalizedOptionViewModel> OnlineCategoryOptionModels => Shell.OnlineCategoryOptionModels;
    public string SelectedOnlineCategory { get => Shell.SelectedOnlineCategory; set => Shell.SelectedOnlineCategory = value; }
    public LocalizedOptionViewModel? SelectedOnlineCategoryOption { get => Shell.SelectedOnlineCategoryOption; set => Shell.SelectedOnlineCategoryOption = value; }
    public IReadOnlyList<string> OnlineSortOptions => Shell.OnlineSortOptions;
    public string SelectedOnlineSort { get => Shell.SelectedOnlineSort; set => Shell.SelectedOnlineSort = value; }
    public bool OnlineCapabilityVf { get => Shell.OnlineCapabilityVf; set => Shell.OnlineCapabilityVf = value; }
    public bool OnlineCapabilityWoff2 { get => Shell.OnlineCapabilityWoff2; set => Shell.OnlineCapabilityWoff2 = value; }
    public string OnlineStatus { get => Shell.OnlineStatus; set => Shell.OnlineStatus = value; }
    public bool IsOnlineSearchBusy { get => Shell.IsOnlineSearchBusy; set => Shell.IsOnlineSearchBusy = value; }
    public ObservableCollection<RemoteFontFamilyItemViewModel> RemoteFonts => Shell.RemoteFonts;
    public RemoteFontFamilyItemViewModel? SelectedRemoteFont { get => Shell.SelectedRemoteFont; set => Shell.SelectedRemoteFont = value; }
    public bool HasRemoteFonts => Shell.HasRemoteFonts;
    public bool HasSelectedRemoteFont => Shell.HasSelectedRemoteFont;
    public bool DownloadFavorite { get => Shell.DownloadFavorite; set => Shell.DownloadFavorite = value; }
    public bool DownloadInstallForCurrentUser { get => Shell.DownloadInstallForCurrentUser; set => Shell.DownloadInstallForCurrentUser = value; }
    public bool DownloadTemporarilyActivate { get => Shell.DownloadTemporarilyActivate; set => Shell.DownloadTemporarilyActivate = value; }
    public string DownloadTagsText { get => Shell.DownloadTagsText; set => Shell.DownloadTagsText = value; }
    public string DownloadCollectionsText { get => Shell.DownloadCollectionsText; set => Shell.DownloadCollectionsText = value; }
    public bool HasOnlineDownloadQueue => Shell.HasOnlineDownloadQueue;
    public ObservableCollection<OnlineFontDownloadQueueItemViewModel> OnlineDownloadQueue => Shell.OnlineDownloadQueue;

    public string GlyphBrowserTitle => Shell.GlyphBrowserTitle;
    public string GlyphBrowserDescription => Shell.GlyphBrowserDescription;
    public string GlyphSearchText { get => Shell.GlyphSearchText; set => Shell.GlyphSearchText = value; }
    public ObservableCollection<string> UnicodeBlocks => Shell.UnicodeBlocks;
    public string SelectedUnicodeBlock { get => Shell.SelectedUnicodeBlock; set => Shell.SelectedUnicodeBlock = value; }
    public string GlyphPageLabel => Shell.GlyphPageLabel;
    public string GlyphStatus { get => Shell.GlyphStatus; set => Shell.GlyphStatus = value; }
    public bool HasGlyphs => Shell.HasGlyphs;
    public ObservableCollection<GlyphItemViewModel> Glyphs => Shell.Glyphs;
    public GlyphItemViewModel? SelectedGlyph { get => Shell.SelectedGlyph; set => Shell.SelectedGlyph = value; }

    public ObservableCollection<MergeStepItemViewModel> MergeSteps => Shell.MergeSteps;
    public bool IsMergeStepSelectFonts => Shell.IsMergeStepSelectFonts;
    public bool IsMergeStepRanges => Shell.IsMergeStepRanges;
    public bool IsMergeStepPreview => Shell.IsMergeStepPreview;
    public bool IsMergeStepExport => Shell.IsMergeStepExport;
    public bool IsMergeStepReport => Shell.IsMergeStepReport;
    public string MergeBaseSearchText { get => Shell.MergeBaseSearchText; set => Shell.MergeBaseSearchText = value; }
    public ObservableCollection<FontFamilyItemViewModel> MergeBaseFonts => Shell.MergeBaseFonts;
    public FontFamilyItemViewModel? SelectedMergeBaseFont { get => Shell.SelectedMergeBaseFont; set => Shell.SelectedMergeBaseFont = value; }
    public string MergeSupplementalSearchText { get => Shell.MergeSupplementalSearchText; set => Shell.MergeSupplementalSearchText = value; }
    public ObservableCollection<FontFamilyItemViewModel> MergeSupplementalFonts => Shell.MergeSupplementalFonts;
    public FontFamilyItemViewModel? SelectedMergeSupplementalFont { get => Shell.SelectedMergeSupplementalFont; set => Shell.SelectedMergeSupplementalFont = value; }
    public string MergeUnicodeRanges { get => Shell.MergeUnicodeRanges; set => Shell.MergeUnicodeRanges = value; }
    public ObservableCollection<MergeUnicodeRangePresetViewModel> MergeUnicodeRangePresets => Shell.MergeUnicodeRangePresets;
    public ObservableCollection<LocalizedOptionViewModel> MergeModeOptionModels => Shell.MergeModeOptionModels;
    public string SelectedMergeModeLabel { get => Shell.SelectedMergeModeLabel; set => Shell.SelectedMergeModeLabel = value; }
    public LocalizedOptionViewModel? SelectedMergeModeOptionByValue { get => Shell.SelectedMergeModeOptionByValue; set => Shell.SelectedMergeModeOptionByValue = value; }
    public string MergeModeDescription => Shell.MergeModeDescription;
    public string MergePreviewSummary => Shell.MergePreviewSummary;
    public string MergeDefaultStrategy => Shell.MergeDefaultStrategy;
    public string MergeStatus { get => Shell.MergeStatus; set => Shell.MergeStatus = value; }
    public ObservableCollection<MergeConflictItemViewModel> MergeConflicts => Shell.MergeConflicts;
    public bool HasMergeConflicts => Shell.HasMergeConflicts;
    public ObservableCollection<MergeIssueItemViewModel> MergeIssues => Shell.MergeIssues;
    public bool HasMergeIssues => Shell.HasMergeIssues;
    public bool MergeLicenseConfirmed { get => Shell.MergeLicenseConfirmed; set => Shell.MergeLicenseConfirmed = value; }
    public string MergeOutputFontName { get => Shell.MergeOutputFontName; set => Shell.MergeOutputFontName = value; }
    public string MergeOutputPath { get => Shell.MergeOutputPath; set => Shell.MergeOutputPath = value; }
    public string MergeReportTitle => Shell.MergeReportTitle;
    public string MergeReportBadge => Shell.MergeReportBadge;
    public string MergeReportInputFonts => Shell.MergeReportInputFonts;
    public string MergeReportRangeLabel => Shell.MergeReportRangeLabel;
    public string MergeReportOutputPath => Shell.MergeReportOutputPath;
    public string MergeReportLicenseTime => Shell.MergeReportLicenseTime;
    public string MergeReportStatsLabel => Shell.MergeReportStatsLabel;
    public string MergeReportErrorMessage => Shell.MergeReportErrorMessage;
    public string MergeReportPath => Shell.MergeReportPath;
    public bool IsMergeBusy { get => Shell.IsMergeBusy; set => Shell.IsMergeBusy = value; }
    public int MergeProgressPercent => Shell.MergeProgressPercent;
    public bool CanGoPreviousMergeStep => Shell.CanGoPreviousMergeStep;
    public string MergeProgressLabel => Shell.MergeProgressLabel;
    public bool CanCancelMerge => Shell.CanCancelMerge;
    public string MergeNextActionLabel => Shell.MergeNextActionLabel;
    public bool IsMergeRangeDialogOpen { get => Shell.IsMergeRangeDialogOpen; set => Shell.IsMergeRangeDialogOpen = value; }
    public string MergeRangeBaseSummary => Shell.MergeRangeBaseSummary;
    public string MergeRangeSupplementalSummary => Shell.MergeRangeSupplementalSummary;
    public ObservableCollection<MergeRangeBlockItemViewModel> MergeRangeBlocks => Shell.MergeRangeBlocks;
    public MergeRangeBlockItemViewModel? SelectedMergeRangeBlock { get => Shell.SelectedMergeRangeBlock; set => Shell.SelectedMergeRangeBlock = value; }
    public bool HasMergeRangeBlocks => Shell.HasMergeRangeBlocks;
    public string MergeRangeSelectedCountLabel => Shell.MergeRangeSelectedCountLabel;
    public string MergeRangeDialogStatus { get => Shell.MergeRangeDialogStatus; set => Shell.MergeRangeDialogStatus = value; }
    public bool IsMergeRangeDialogBusy => Shell.IsMergeRangeDialogBusy;
    public bool IsMergeRangeEmptyVisible => Shell.IsMergeRangeEmptyVisible;
    public bool IsMergeRangeBlockEmptyVisible => Shell.IsMergeRangeBlockEmptyVisible;
    public IReadOnlyList<MergeRangeSegmentItemViewModel> MergeRangeSegments => Shell.MergeRangeSegments;
    public bool HasMergeRangeSegments => Shell.HasMergeRangeSegments;
    public bool CanApplyMergeRangeSelection => Shell.CanApplyMergeRangeSelection;

    public string GoogleFontsApiKeyStatus => Shell.GoogleFontsApiKeyStatus;
    public string GoogleFontsApiKeyText { get => Shell.GoogleFontsApiKeyText; set => Shell.GoogleFontsApiKeyText = value; }
    public string AppUpdateCurrentVersion => Shell.AppUpdateCurrentVersion;
    public string AppUpdateSource => Shell.AppUpdateSource;
    public string AppUpdateStatus => Shell.AppUpdateStatus;
    public string AppUpdateAvailableVersion => Shell.AppUpdateAvailableVersion;
    public string AppUpdateReleaseNotes => Shell.AppUpdateReleaseNotes;
    public int AppUpdateProgress => Shell.AppUpdateProgress;
    public string AppUpdateProgressLabel => Shell.AppUpdateProgressLabel;
    public bool IsAppUpdateBusy => Shell.IsAppUpdateBusy;
    public bool CanDownloadAppUpdate => Shell.CanDownloadAppUpdate;
    public bool HasAppUpdateReleaseNotes => Shell.HasAppUpdateReleaseNotes;
    public ObservableCollection<LanguageOptionViewModel> LanguageOptions => Shell.LanguageOptions;
    public LanguageOptionViewModel? SelectedLanguage { get => Shell.SelectedLanguage; set => Shell.SelectedLanguage = value; }
    public IReadOnlyList<string> CompatibilityMatrix => Shell.CompatibilityMatrix;
    public ObservableCollection<OperationLogItemViewModel> OperationLogs => Shell.OperationLogs;

    public string CurrentPageTitle => Shell.CurrentPageTitle;
    public string CurrentPageDescription => Shell.CurrentPageDescription;
    public string CurrentPageMilestone => Shell.CurrentPageMilestone;

    public ICommand RescanCommand => Shell.RescanCommand;
    public ICommand OpenImportDialogCommand => Shell.OpenImportDialogCommand;
    public ICommand OpenTagsDialogCommand => Shell.OpenTagsDialogCommand;
    public ICommand SetPreviewModeCommand => Shell.SetPreviewModeCommand;
    public ICommand ToggleTemporaryActivationCommand => Shell.ToggleTemporaryActivationCommand;
    public ICommand ToggleFavoriteCommand => Shell.ToggleFavoriteCommand;
    public ICommand ChooseManagedDirectoryCommand => Shell.ChooseManagedDirectoryCommand;
    public ICommand CloseImportDialogCommand => Shell.CloseImportDialogCommand;
    public ICommand StartImportCommand => Shell.StartImportCommand;
    public ICommand OpenDeleteTagDialogCommand => Shell.OpenDeleteTagDialogCommand;
    public ICommand CloseTagsDialogCommand => Shell.CloseTagsDialogCommand;
    public ICommand SaveTagsDialogCommand => Shell.SaveTagsDialogCommand;
    public ICommand CloseDeleteTagDialogCommand => Shell.CloseDeleteTagDialogCommand;
    public ICommand ConfirmDeleteTagCommand => Shell.ConfirmDeleteTagCommand;
    public ICommand InstallSelectedFontCommand => Shell.InstallSelectedFontCommand;
    public ICommand OpenUninstallDialogCommand => Shell.OpenUninstallDialogCommand;
    public ICommand OpenGlyphBrowserCommand => Shell.OpenGlyphBrowserCommand;
    public ICommand CloseUninstallDialogCommand => Shell.CloseUninstallDialogCommand;
    public ICommand ConfirmUninstallCommand => Shell.ConfirmUninstallCommand;
    public ICommand CreateCollectionCommand => Shell.CreateCollectionCommand;
    public ICommand ActivateSelectedCollectionCommand => Shell.ActivateSelectedCollectionCommand;
    public ICommand DeactivateSelectedCollectionCommand => Shell.DeactivateSelectedCollectionCommand;
    public ICommand ExportSelectedCollectionCommand => Shell.ExportSelectedCollectionCommand;
    public ICommand OpenDeleteCollectionDialogCommand => Shell.OpenDeleteCollectionDialogCommand;
    public ICommand RemoveFontFromCollectionCommand => Shell.RemoveFontFromCollectionCommand;
    public ICommand CloseDeleteCollectionDialogCommand => Shell.CloseDeleteCollectionDialogCommand;
    public ICommand ConfirmDeleteCollectionCommand => Shell.ConfirmDeleteCollectionCommand;
    public ICommand SearchOnlineFontsCommand => Shell.SearchOnlineFontsCommand;
    public ICommand DownloadSelectedOnlineFontCommand => Shell.DownloadSelectedOnlineFontCommand;
    public ICommand RetryOnlineDownloadCommand => Shell.RetryOnlineDownloadCommand;
    public ICommand BackFromGlyphBrowserCommand => Shell.BackFromGlyphBrowserCommand;
    public ICommand PreviousGlyphPageCommand => Shell.PreviousGlyphPageCommand;
    public ICommand NextGlyphPageCommand => Shell.NextGlyphPageCommand;
    public ICommand CopySelectedGlyphCharacterCommand => Shell.CopySelectedGlyphCharacterCommand;
    public ICommand CopySelectedGlyphUnicodeCommand => Shell.CopySelectedGlyphUnicodeCommand;
    public ICommand LoadMergeReportCommand => Shell.LoadMergeReportCommand;
    public ICommand SetMergeStepCommand => Shell.SetMergeStepCommand;
    public ICommand OpenMergeRangeDialogCommand => Shell.OpenMergeRangeDialogCommand;
    public ICommand ApplyMergeUnicodeBlockCommand => Shell.ApplyMergeUnicodeBlockCommand;
    public ICommand ApplyMergeUnicodePresetCommand => Shell.ApplyMergeUnicodePresetCommand;
    public ICommand PreviousMergeStepCommand => Shell.PreviousMergeStepCommand;
    public ICommand CancelMergeCommand => Shell.CancelMergeCommand;
    public ICommand NextMergeStepCommand => Shell.NextMergeStepCommand;
    public ICommand ChooseMergeOutputPathCommand => Shell.ChooseMergeOutputPathCommand;
    public ICommand CloseMergeRangeDialogCommand => Shell.CloseMergeRangeDialogCommand;
    public ICommand ApplyMergeRangeSelectionCommand => Shell.ApplyMergeRangeSelectionCommand;
    public ICommand SaveGoogleFontsApiKeyCommand => Shell.SaveGoogleFontsApiKeyCommand;
    public ICommand CheckForAppUpdatesCommand => Shell.CheckForAppUpdatesCommand;
    public ICommand DownloadAndRestartAppUpdateCommand => Shell.DownloadAndRestartAppUpdateCommand;
    public ICommand DismissAppUpdateCommand => Shell.DismissAppUpdateCommand;
}
