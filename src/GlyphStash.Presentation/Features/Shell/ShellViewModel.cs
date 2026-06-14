using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlyphStash.Application.Abstractions.App;
using GlyphStash.Application.Abstractions.Fonts;
using GlyphStash.Application.Fonts;
using GlyphStash.Domain.Fonts;
using GlyphStash.Localization;
using static GlyphStash.Localization.AppTextExtensions;
using GlyphStash.Presentation.Features.Collections;
using GlyphStash.Presentation.Features.FontLibrary;
using GlyphStash.Presentation.Features.GlyphBrowser;
using GlyphStash.Presentation.Features.MergeTool;
using GlyphStash.Presentation.Features.OnlineFonts;
using GlyphStash.Presentation.Features.Settings;
using GlyphStash.Presentation.Features.Shell;
using GlyphStash.Presentation.Services;
using DomainUnicodeRange = GlyphStash.Domain.Fonts.UnicodeRange;

namespace GlyphStash.Presentation.ViewModels;

public sealed partial class ShellViewModel : ObservableObject, IDisposable
{
    private const string AllSourceFilter = "全部来源";
    private const string AllStateFilter = "全部状态";
    private const string AllTagFilter = "全部标签";
    private const string AllCollectionFilter = "全部集合";
    private const string AllOnlineSubset = "全部子集";
    private const string AllOnlineCategory = "全部分类";
    private const string SupplementMergeModeLabel = "补全（追加）";
    private const string OverwriteMergeModeLabel = "覆盖";
    private const string DefaultPreviewTextZh = "GlyphStash 字体预览 Aa 123 你好";
    private const string DefaultPreviewTextEn = "GlyphStash font preview Aa 123 Hello";

    private readonly IFontLibraryService _fontLibraryService;
    private readonly ILocalFontManagementService _localManagementService;
    private readonly IUserFileDialogService _fileDialogService;
    private readonly IUserClipboardService _clipboardService;
    private readonly IFontPreviewRegistry _fontPreviewRegistry;
    private readonly IAppLocalizationService _localizationService;
    private readonly EventHandler? _localizationCultureChangedHandler;
    private readonly IOnlineFontService _onlineFontService;
    private readonly IGlyphCatalogService _glyphCatalogService;
    private readonly IFontMergeService _fontMergeService;
    private readonly IAppUpdateService _appUpdateService;
    private readonly List<FontFamilyItemViewModel> _allFonts = [];
    private FontImportPreview? _currentImportPreview;
    private bool _isProcessingOnlineDownloadQueue;
    private FontFaceItemViewModel? _currentGlyphFace;
    private FontMergePreview? _currentMergePreview;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private CancellationTokenSource? _languageSaveCancellation;
    private CancellationTokenSource? _glyphLoadCancellation;
    private CancellationTokenSource? _collectionReloadCancellation;
    private CancellationTokenSource? _onlineDownloadCancellation;
    private CancellationTokenSource? _mergeCancellation;
    private readonly List<MergeRangeSegmentItemViewModel> _allMergeRangeSegments = [];
    private readonly FontLibraryViewModel _fontLibraryPage;
    private readonly CollectionsViewModel _collectionsPage;
    private readonly OnlineFontsViewModel _onlineFontsPage;
    private readonly MergeToolViewModel _mergeToolPage;
    private readonly GlyphBrowserViewModel _glyphBrowserPage;
    private readonly SettingsViewModel _settingsPage;
    private readonly PlaceholderPageViewModel _placeholderPage;

    [ObservableProperty]
    private NavigationItemViewModel? _selectedNavigationItem;

    [ObservableProperty]
    private ShellFeatureViewModel? _currentPage;

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private string _selectedSourceFilter = AllSourceFilter;

    [ObservableProperty]
    private LocalizedOptionViewModel? _selectedSourceFilterOption;

    [ObservableProperty]
    private string _selectedStateFilter = AllStateFilter;

    [ObservableProperty]
    private LocalizedOptionViewModel? _selectedStateFilterOption;

    [ObservableProperty]
    private string _selectedTagFilter = AllTagFilter;

    [ObservableProperty]
    private LocalizedOptionViewModel? _selectedTagFilterOption;

    [ObservableProperty]
    private string _selectedCollectionFilter = AllCollectionFilter;

    [ObservableProperty]
    private LocalizedOptionViewModel? _selectedCollectionFilterOption;

    [ObservableProperty]
    private string _previewText = AppText.TranslateLiteral(DefaultPreviewTextZh);

    [ObservableProperty]
    private double _previewFontSize = 30;

    [ObservableProperty]
    private string _previewMode = "单行";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedFont))]
    [NotifyPropertyChangedFor(nameof(HasNoSelectedFont))]
    private FontFamilyItemViewModel? _selectedFont;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedPreviewFace))]
    [NotifyPropertyChangedFor(nameof(SelectedPreviewFontFamily))]
    [NotifyPropertyChangedFor(nameof(SelectedPreviewFontWeight))]
    [NotifyPropertyChangedFor(nameof(SelectedPreviewFontStyle))]
    private FontFaceItemViewModel? _selectedPreviewFace;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanHideToTray))]
    [NotifyPropertyChangedFor(nameof(TrayHideBlockReason))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDownloadAppUpdate))]
    [NotifyPropertyChangedFor(nameof(AppUpdateProgressLabel))]
    private bool _isAppUpdateBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDownloadAppUpdate))]
    private bool _hasPendingAppUpdate;

    [ObservableProperty]
    private string _appUpdateStatus = AppText.TranslateLiteral("尚未检查应用更新。");

    [ObservableProperty]
    private string _appUpdateAvailableVersion = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAppUpdateReleaseNotes))]
    private string _appUpdateReleaseNotes = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AppUpdateProgressLabel))]
    private int _appUpdateProgress;

    [ObservableProperty]
    private bool _isImportDialogOpen;

    [ObservableProperty]
    private bool _isTagsDialogOpen;

    [ObservableProperty]
    private bool _isUninstallDialogOpen;

    [ObservableProperty]
    private bool _isDeleteCollectionDialogOpen;

    [ObservableProperty]
    private bool _isDeleteTagDialogOpen;

    [ObservableProperty]
    private string _pendingDeleteTagName = "";

    [ObservableProperty]
    private string _scanStatus = AppText.TranslateLiteral("准备加载字体索引");

    [ObservableProperty]
    private string _statusMessage = AppText.TranslateLiteral("最近操作：等待字体索引");

    [ObservableProperty]
    private string _toastMessage = "";

    [ObservableProperty]
    private bool _isToastVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasManagedFontDirectory))]
    [NotifyPropertyChangedFor(nameof(ManagedDirectoryStatus))]
    private string _managedFontDirectory = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GoogleFontsApiKeyStatus))]
    private string _googleFontsApiKeyText = "";

    [ObservableProperty]
    private string _importStatus = AppText.TranslateLiteral("请选择字体文件并选择 GlyphStash 管理目录。");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanImportTemporarilyActivate))]
    [NotifyPropertyChangedFor(nameof(ImportTemporaryActivationReason))]
    private bool _importInstallForCurrentUser;

    [ObservableProperty]
    private bool _importTemporarilyActivate = true;

    [ObservableProperty]
    private string _importTagsText = "";

    [ObservableProperty]
    private string _importCollectionsText = "";

    [ObservableProperty]
    private string _tagEditorText = "";

    [ObservableProperty]
    private string _collectionEditorText = "";

    [ObservableProperty]
    private string _collectionSearchText = "";

    [ObservableProperty]
    private CollectionItemViewModel? _selectedCollection;

    [ObservableProperty]
    private string _newCollectionName = "";

    [ObservableProperty]
    private string _onlineSearchText = "Noto Sans";

    [ObservableProperty]
    private string _selectedOnlineSubset = AllOnlineSubset;

    [ObservableProperty]
    private LocalizedOptionViewModel? _selectedOnlineSubsetOption;

    [ObservableProperty]
    private string _selectedOnlineCategory = AllOnlineCategory;

    [ObservableProperty]
    private LocalizedOptionViewModel? _selectedOnlineCategoryOption;

    [ObservableProperty]
    private string _selectedOnlineSort = "alpha";

    [ObservableProperty]
    private bool _onlineCapabilityVf;

    [ObservableProperty]
    private bool _onlineCapabilityWoff2;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedRemoteFont))]
    private RemoteFontFamilyItemViewModel? _selectedRemoteFont;

    [ObservableProperty]
    private string _onlineStatus = AppText.TranslateLiteral("请在设置页配置 Google Fonts API key 后搜索在线字体。");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanHideToTray))]
    [NotifyPropertyChangedFor(nameof(TrayHideBlockReason))]
    private bool _isOnlineSearchBusy;

    [ObservableProperty]
    private bool _downloadInstallForCurrentUser;

    [ObservableProperty]
    private bool _downloadTemporarilyActivate;

    [ObservableProperty]
    private bool _downloadFavorite;

    [ObservableProperty]
    private string _downloadTagsText = "";

    [ObservableProperty]
    private string _downloadCollectionsText = "";

    [ObservableProperty]
    private bool _isGlyphBrowserOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanHideToTray))]
    [NotifyPropertyChangedFor(nameof(TrayHideBlockReason))]
    private bool _isGlyphLoading;

    [ObservableProperty]
    private string _glyphSearchText = "";

    [ObservableProperty]
    private string _selectedUnicodeBlock = "全部区块";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedGlyph))]
    private GlyphItemViewModel? _selectedGlyph;

    [ObservableProperty]
    private string _glyphStatus = AppText.TranslateLiteral("请从字体详情进入字形浏览。");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GlyphPageLabel))]
    private int _glyphPageNumber = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GlyphPageLabel))]
    private int _glyphTotalPages = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMergeStepSelectFonts))]
    [NotifyPropertyChangedFor(nameof(IsMergeStepRanges))]
    [NotifyPropertyChangedFor(nameof(IsMergeStepPreview))]
    [NotifyPropertyChangedFor(nameof(IsMergeStepExport))]
    [NotifyPropertyChangedFor(nameof(IsMergeStepReport))]
    [NotifyPropertyChangedFor(nameof(CanGoPreviousMergeStep))]
    [NotifyPropertyChangedFor(nameof(MergeNextActionLabel))]
    private int _mergeStepIndex;

    [ObservableProperty]
    private string _mergeBaseSearchText = "";

    [ObservableProperty]
    private string _mergeSupplementalSearchText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedMergeBaseFont))]
    private FontFamilyItemViewModel? _selectedMergeBaseFont;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedMergeSupplementalFont))]
    private FontFamilyItemViewModel? _selectedMergeSupplementalFont;

    [ObservableProperty]
    private string _mergeUnicodeRanges = "U+4E00-U+9FFF";

    [ObservableProperty]
    private bool _isMergeRangeDialogOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMergeRangeEmptyVisible))]
    [NotifyPropertyChangedFor(nameof(IsMergeRangeBlockEmptyVisible))]
    [NotifyPropertyChangedFor(nameof(CanApplyMergeRangeSelection))]
    [NotifyPropertyChangedFor(nameof(CanHideToTray))]
    [NotifyPropertyChangedFor(nameof(TrayHideBlockReason))]
    private bool _isMergeRangeDialogBusy;

    [ObservableProperty]
    private string _mergeRangeDialogStatus = AppText.TranslateLiteral("请选择基础字体和补充字体。");

    [ObservableProperty]
    private string _mergeRangeBaseSummary = AppText.TranslateLiteral("未读取基础字体 A。");

    [ObservableProperty]
    private string _mergeRangeSupplementalSummary = AppText.TranslateLiteral("未读取补充字体 B。");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MergeRangeSegments))]
    [NotifyPropertyChangedFor(nameof(HasMergeRangeSegments))]
    [NotifyPropertyChangedFor(nameof(IsMergeRangeBlockEmptyVisible))]
    private MergeRangeBlockItemViewModel? _selectedMergeRangeBlock;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedMergeMode))]
    [NotifyPropertyChangedFor(nameof(MergeModeDescription))]
    private string _selectedMergeModeLabel = SupplementMergeModeLabel;

    [ObservableProperty]
    private LocalizedOptionViewModel? _selectedMergeModeOptionByValue;

    [ObservableProperty]
    private LocalizedOptionViewModel? _selectedMergeModeOption;

    [ObservableProperty]
    private bool _mergeLicenseConfirmed;

    [ObservableProperty]
    private string _mergeOutputFontName = "";

    [ObservableProperty]
    private string _mergeOutputPath = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MergeProgressLabel))]
    private string _mergeStatus = AppText.TranslateLiteral("请选择基础字体和补充字体。");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MergeProgressLabel))]
    [NotifyPropertyChangedFor(nameof(CanGoPreviousMergeStep))]
    [NotifyPropertyChangedFor(nameof(CanCancelMerge))]
    [NotifyPropertyChangedFor(nameof(CanHideToTray))]
    [NotifyPropertyChangedFor(nameof(TrayHideBlockReason))]
    private bool _isMergeBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MergeProgressLabel))]
    private int _mergeProgressPercent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MergeProgressLabel))]
    private string _mergeProgressStage = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MergeProgressLabel))]
    private string _mergeProgressMessage = "";

    [ObservableProperty]
    private string _mergeReportTitle = AppText.TranslateLiteral("等待导出");

    [ObservableProperty]
    private string _mergeReportBadge = AppText.TranslateLiteral("待生成");

    [ObservableProperty]
    private string _mergeReportOutputPath = "";

    [ObservableProperty]
    private string _mergeReportInputFonts = "";

    [ObservableProperty]
    private string _mergeReportRangeLabel = "";

    [ObservableProperty]
    private string _mergeReportStatsLabel = "";

    [ObservableProperty]
    private string _mergeReportLicenseTime = "";

    [ObservableProperty]
    private string _mergeReportErrorMessage = "";

    [ObservableProperty]
    private string _mergeReportPath = "";

    private LanguageOptionViewModel? _selectedLanguage;
    private bool _isSynchronizingOptionSelection;

    public LanguageOptionViewModel? SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (SetProperty(ref _selectedLanguage, value))
            {
                OnSelectedLanguageChanged(value);
            }
        }
    }

    public ShellViewModel(
        IFontLibraryService fontLibraryService,
        ILocalFontManagementService localManagementService,
        IUserFileDialogService fileDialogService,
        IOnlineFontService onlineFontService,
        IGlyphCatalogService glyphCatalogService,
        IUserClipboardService clipboardService,
        IFontPreviewRegistry fontPreviewRegistry,
        IFontMergeService fontMergeService,
        IAppUpdateService appUpdateService,
        IAppLocalizationService localizationService)
        : this(
            fontLibraryService,
            localManagementService,
            fileDialogService,
            onlineFontService,
            glyphCatalogService,
            clipboardService,
            fontPreviewRegistry,
            fontMergeService,
            appUpdateService,
            localizationService,
            new FontLibraryViewModel(),
            new CollectionsViewModel(),
            new OnlineFontsViewModel(),
            new MergeToolViewModel(),
            new GlyphBrowserViewModel(),
            new SettingsViewModel(),
            new PlaceholderPageViewModel())
    {
    }

    public ShellViewModel(
        IFontLibraryService fontLibraryService,
        ILocalFontManagementService localManagementService,
        IUserFileDialogService fileDialogService,
        IOnlineFontService onlineFontService,
        IGlyphCatalogService glyphCatalogService,
        IUserClipboardService clipboardService,
        IFontPreviewRegistry fontPreviewRegistry,
        IFontMergeService fontMergeService,
        IAppUpdateService appUpdateService,
        IAppLocalizationService localizationService,
        FontLibraryViewModel fontLibraryPage,
        CollectionsViewModel collectionsPage,
        OnlineFontsViewModel onlineFontsPage,
        MergeToolViewModel mergeToolPage,
        GlyphBrowserViewModel glyphBrowserPage,
        SettingsViewModel settingsPage,
        PlaceholderPageViewModel placeholderPage)
    {
        _fontLibraryService = fontLibraryService;
        _localManagementService = localManagementService;
        _fileDialogService = fileDialogService;
        _onlineFontService = onlineFontService;
        _glyphCatalogService = glyphCatalogService;
        _fontMergeService = fontMergeService;
        _clipboardService = clipboardService;
        _fontPreviewRegistry = fontPreviewRegistry;
        _appUpdateService = appUpdateService;
        _localizationService = localizationService;
        _fontLibraryPage = fontLibraryPage;
        _collectionsPage = collectionsPage;
        _onlineFontsPage = onlineFontsPage;
        _mergeToolPage = mergeToolPage;
        _glyphBrowserPage = glyphBrowserPage;
        _settingsPage = settingsPage;
        _placeholderPage = placeholderPage;
        _fontLibraryPage.Attach(this);
        _collectionsPage.Attach(this);
        _onlineFontsPage.Attach(this);
        _mergeToolPage.Attach(this);
        _glyphBrowserPage.Attach(this);
        _settingsPage.Attach(this);
        _placeholderPage.Attach(this);
        CurrentPage = _fontLibraryPage;
        InitializeLocalizedOptions();
        foreach (var language in _localizationService.SupportedLanguages)
        {
            LanguageOptions.Add(new LanguageOptionViewModel(language));
        }

        ApplyCurrentLanguageSelection();

        _localizationCultureChangedHandler = (_, _) => RefreshLocalizedState();
        _localizationService.CultureChanged += _localizationCultureChangedHandler;
        RefreshMergeStepState();
    }

    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        _lifetimeCancellation.Cancel();
        if (_localizationCultureChangedHandler is not null)
        {
            _localizationService.CultureChanged -= _localizationCultureChangedHandler;
        }

        CancelAndDispose(ref _languageSaveCancellation);
        CancelAndDispose(ref _glyphLoadCancellation);
        CancelAndDispose(ref _collectionReloadCancellation);
        CancelAndDispose(ref _onlineDownloadCancellation);
        _mergeCancellation?.Cancel();
        _mergeCancellation?.Dispose();
        _mergeCancellation = null;
        _lifetimeCancellation.Dispose();
    }

    public bool IsDisposed { get; private set; }

    public void NotifyTrayHideBlocked(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return;
        }

        StatusMessage = reason;
        ShowToast(reason);
    }

    public ObservableCollection<FontFamilyItemViewModel> Fonts { get; } = [];

    public ObservableCollection<CollectionItemViewModel> Collections { get; } = [];

    public ObservableCollection<FontFamilyItemViewModel> CollectionFonts { get; } = [];

    public ObservableCollection<TagRecord> AvailableTags { get; } = [];

    public ObservableCollection<string> AvailableCollections { get; } = [];

    public ObservableCollection<string> TagFilters { get; } = [AllTagFilter];

    public ObservableCollection<string> CollectionFilters { get; } = [AllCollectionFilter];

    public ObservableCollection<LocalizedOptionViewModel> SourceFilterOptions { get; } = [];

    public ObservableCollection<LocalizedOptionViewModel> StateFilterOptions { get; } = [];

    public ObservableCollection<LocalizedOptionViewModel> TagFilterOptions { get; } = [];

    public ObservableCollection<LocalizedOptionViewModel> CollectionFilterOptions { get; } = [];

    public ObservableCollection<LocalizedOptionViewModel> OnlineSubsetOptionModels { get; } = [];

    public ObservableCollection<LocalizedOptionViewModel> OnlineCategoryOptionModels { get; } = [];

    public ObservableCollection<NameOptionViewModel> TagOptions { get; } = [];

    public ObservableCollection<NameOptionViewModel> CollectionOptions { get; } = [];

    public ObservableCollection<ImportPreviewItemViewModel> ImportPreviewItems { get; } = [];

    public ObservableCollection<OperationLogItemViewModel> OperationLogs { get; } = [];

    public ObservableCollection<RemoteFontFamilyItemViewModel> RemoteFonts { get; } = [];

    public ObservableCollection<OnlineFontDownloadQueueItemViewModel> OnlineDownloadQueue { get; } = [];

    public ObservableCollection<GlyphItemViewModel> Glyphs { get; } = [];

    public ObservableCollection<string> UnicodeBlocks { get; } = ["全部区块"];

    public ObservableCollection<FontFamilyItemViewModel> MergeBaseFonts { get; } = [];

    public ObservableCollection<FontFamilyItemViewModel> MergeSupplementalFonts { get; } = [];

    public ObservableCollection<MergeUnicodeRangePresetViewModel> MergeUnicodeRangePresets { get; } =
        [.. MergeUnicodeRangePresetCatalog.Presets.Select(preset => new MergeUnicodeRangePresetViewModel(preset))];

    public ObservableCollection<MergeRangeBlockItemViewModel> MergeRangeBlocks { get; } = [];

    public IReadOnlyList<MergeRangeSegmentItemViewModel> MergeRangeSegments => GetVisibleMergeRangeSegments().ToList();

    public ObservableCollection<MergeStepItemViewModel> MergeSteps { get; } =
    [
        new(0, "1 选择字体"),
        new(1, "2 指定范围"),
        new(2, "3 预览冲突"),
        new(3, "4 授权与导出"),
        new(4, "5 报告")
    ];

    public ObservableCollection<MergeConflictItemViewModel> MergeConflicts { get; } = [];

    public ObservableCollection<MergeIssueItemViewModel> MergeIssues { get; } = [];

    public ObservableCollection<LanguageOptionViewModel> LanguageOptions { get; } = [];

    public ObservableCollection<LocalizedOptionViewModel> MergeModeOptionModels { get; } = [];

    public ObservableCollection<NavigationItemViewModel> NavigationItems { get; } =
    [
        new("font-library", "字体库", "字体库", "查看、搜索和预览本地字体。", "M2", true),
        new("collections", "集合", "集合", "项目字体包、批量临时启用、关闭与导出清单。", "M2", true),
        new("online-fonts", "在线字体", "在线字体", "Google Fonts 搜索、授权信息与下载流程。", "M3", true),
        new("merge-tool", "合并工具", "合并工具", "指定 Unicode 范围合并字形并导出报告。", "M4", true),
        new("settings", "设置", "设置", "管理目录、临时字体兼容性矩阵和诊断日志。", "M2", true)
    ];

    public IReadOnlyList<string> SourceFilters { get; } =
    [
        AllSourceFilter,
        "系统字体",
        "用户级安装",
        "GlyphStash 管理",
        "临时字体"
    ];

    public IReadOnlyList<string> StateFilters { get; } =
    [
        AllStateFilter,
        "已安装",
        "已临时启用",
        "未启用"
    ];

    public IReadOnlyList<string> PreviewModes { get; } = ["单行", "段落", "字符集"];

    public IReadOnlyList<string> OnlineSubsetOptions { get; } =
    [
        AllOnlineSubset,
        "latin",
        "latin-ext",
        "cyrillic",
        "cyrillic-ext",
        "greek",
        "greek-ext",
        "vietnamese",
        "arabic",
        "hebrew",
        "devanagari",
        "thai",
        "korean",
        "japanese",
        "chinese-simplified",
        "chinese-traditional"
    ];

    public IReadOnlyList<string> OnlineCategoryOptions { get; } =
    [
        AllOnlineCategory,
        "serif",
        "sans-serif",
        "monospace",
        "display",
        "handwriting"
    ];

    public IReadOnlyList<string> OnlineSortOptions { get; } =
    [
        "alpha",
        "date",
        "popularity",
        "style",
        "trending"
    ];

    public IReadOnlyList<string> CompatibilityMatrix =>
    [
        L("记事本：覆盖新启动与已运行应用，验证 WM_FONTCHANGE 后字体菜单刷新。"),
        L("VS Code：覆盖常见开发工具，新启动应可见，已运行窗口可能需要重新打开字体列表。"),
        L("Edge / Chrome：覆盖浏览器与前端预览，验证 CSS font-family 选择。"),
        L("Word / PowerPoint：覆盖 Office 文档场景，已运行应用可能需要重启文档窗口。"),
        L("Figma / Adobe 设计工具：覆盖设计工作流，未安装时标记为未验证。")
    ];

    public bool HasFonts => Fonts.Count > 0;

    public bool IsEmpty => !IsBusy && Fonts.Count == 0;

    public bool HasSelectedFont => SelectedFont is not null;

    public bool HasNoSelectedFont => SelectedFont is null;

    public bool HasSelectedPreviewFace => SelectedPreviewFace is not null;

    public FontFamily SelectedPreviewFontFamily =>
        _fontPreviewRegistry.Resolve(SelectedPreviewFace, SelectedFont?.FamilyName ?? "");

    public FontWeight SelectedPreviewFontWeight => ToFontWeight(SelectedPreviewFace?.Weight ?? 400);

    public FontStyle SelectedPreviewFontStyle =>
        string.Equals(SelectedPreviewFace?.Slant, "Italic", StringComparison.OrdinalIgnoreCase)
            ? FontStyle.Italic
            : FontStyle.Normal;

    public string FontCountLabel => _allFonts.Count == 0
        ? L("字体索引为空")
        : AppText.CurrentCultureCode == AppText.EnglishCultureCode
            ? $"Index ready: {_allFonts.Count:N0} font families"
            : $"索引就绪：{_allFonts.Count:N0} 个字体族";

    public bool IsFontLibraryPage => !IsGlyphBrowserOpen && SelectedNavigationItem?.IsFontLibrary == true;

    public bool IsCollectionsPage => !IsGlyphBrowserOpen && SelectedNavigationItem?.IsCollections == true;

    public bool IsOnlineFontsPage => !IsGlyphBrowserOpen && SelectedNavigationItem?.Key == "online-fonts";

    public bool IsMergeToolPage => !IsGlyphBrowserOpen && SelectedNavigationItem?.Key == "merge-tool";

    public bool IsSettingsPage => !IsGlyphBrowserOpen && SelectedNavigationItem?.IsSettings == true;

    public bool IsPlaceholderPage => !IsGlyphBrowserOpen && SelectedNavigationItem is { IsImplemented: false };

    public bool IsGlyphBrowserPage => IsGlyphBrowserOpen;

    public string CurrentPageTitle => SelectedNavigationItem?.Title ?? L("字体库");

    public string CurrentPageDescription => SelectedNavigationItem?.Description ?? "";

    public string CurrentPageMilestone => SelectedNavigationItem?.Milestone ?? "M2";

    public string EmptyStateMessage => _allFonts.Count == 0
        ? L("字体索引为空。请重新扫描字体。")
        : AppText.CurrentCultureCode == AppText.EnglishCultureCode
            ? $"No fonts match the current filters. The index contains {_allFonts.Count:N0} font families."
            : $"当前筛选没有匹配字体。索引中共有 {_allFonts.Count:N0} 个字体族。";

    public bool HasManagedFontDirectory => !string.IsNullOrWhiteSpace(ManagedFontDirectory);

    public string ManagedDirectoryStatus => HasManagedFontDirectory ? T("Label.ManagedDirectoryReady") : T("Label.ManagedDirectoryRequired");

    public string GoogleFontsApiKeyStatus => string.IsNullOrWhiteSpace(GoogleFontsApiKeyText) ? T("Common.NotConfigured") : T("Common.Configured");

    public string AppUpdateCurrentVersion => _appUpdateService.CurrentVersion;

    public string AppUpdateSource => _appUpdateService.UpdateSource;

    public bool CanDownloadAppUpdate => HasPendingAppUpdate && !IsAppUpdateBusy;

    public bool HasAppUpdateReleaseNotes => !string.IsNullOrWhiteSpace(AppUpdateReleaseNotes);

    public string AppUpdateProgressLabel => IsAppUpdateBusy && AppUpdateProgress > 0
        ? AppText.FormatLiteral("下载进度：{0}%", "Download progress: {0}%", AppUpdateProgress)
        : "";

    public bool HasRemoteFonts => RemoteFonts.Count > 0;

    public bool HasSelectedRemoteFont => SelectedRemoteFont is not null;

    public bool HasOnlineDownloadQueue => OnlineDownloadQueue.Count > 0;

    public bool HasGlyphs => Glyphs.Count > 0;

    public bool HasSelectedGlyph => SelectedGlyph is not null;

    public string GlyphPageLabel => $"{GlyphPageNumber:N0} / {GlyphTotalPages:N0}";

    public string GlyphBrowserTitle =>
        _currentGlyphFace is null
            ? L("字形浏览")
            : AppText.CurrentCultureCode == AppText.EnglishCultureCode
                ? $"Glyph browser: {_currentGlyphFace.FamilyName}"
                : $"字形浏览：{_currentGlyphFace.FamilyName}";

    public string GlyphBrowserDescription =>
        _currentGlyphFace is null
            ? L("查看 Unicode 映射字符，搜索字符或码位，并复制字形信息。")
            : AppText.CurrentCultureCode == AppText.EnglishCultureCode
                ? $"Current style: {_currentGlyphFace.StyleLabel}. View Unicode-mapped characters, search by character or code point, and copy glyph details."
                : $"当前样式：{_currentGlyphFace.StyleLabel}。查看 Unicode 映射字符，搜索字符或码位，并复制字形信息。";

    public bool HasImportPreview => ImportPreviewItems.Count > 0;

    public bool CanImportTemporarilyActivate => !ImportInstallForCurrentUser;

    public string ImportTemporaryActivationReason => ImportInstallForCurrentUser ? L("用户级安装后已安装，无需临时启用") : "";

    public bool HasCollections => Collections.Count > 0;

    public bool HasSelectedCollection => SelectedCollection is not null;

    public string PendingDeleteTagConfirmationText =>
        AppText.FormatLiteral("确定删除标签 {0}？", "Delete tag {0}?", PendingDeleteTagName);

    public string PreviewFontSizeLabel => $"{PreviewFontSize:0}px";

    public IReadOnlyList<string> MergeUnicodeBlockOptions { get; } =
    [
        "U+4E00-U+9FFF",
        "U+0000-U+007F",
        "U+3000-U+303F",
        "U+2000-U+206F"
    ];

    public IReadOnlyList<string> MergeModeOptions { get; } = [SupplementMergeModeLabel, OverwriteMergeModeLabel];

    public FontMergeMode SelectedMergeMode =>
        string.Equals(SelectedMergeModeLabel, OverwriteMergeModeLabel, StringComparison.CurrentCultureIgnoreCase)
            ? FontMergeMode.Overwrite
            : FontMergeMode.Supplement;

    public string MergeModeDescription => SelectedMergeMode == FontMergeMode.Overwrite
        ? L("覆盖模式会用补充字体替换指定范围内基础字体已存在的码位，同时补齐基础字体缺失的码位。")
        : L("补全（追加）模式只合并基础字体缺失、补充字体存在的码位，基础字体已存在的码位会跳过。");

    public bool IsMergeStepSelectFonts => MergeStepIndex == 0;

    public bool IsMergeStepRanges => MergeStepIndex == 1;

    public bool IsMergeStepPreview => MergeStepIndex == 2;

    public bool IsMergeStepExport => MergeStepIndex == 3;

    public bool IsMergeStepReport => MergeStepIndex == 4;

    public bool HasSelectedMergeBaseFont => SelectedMergeBaseFont is not null;

    public bool HasSelectedMergeSupplementalFont => SelectedMergeSupplementalFont is not null;

    public bool HasMergePreview => _currentMergePreview is not null;

    public bool HasMergeConflicts => MergeConflicts.Count > 0;

    public bool HasMergeIssues => MergeIssues.Count > 0;

    public bool HasMergeRangeBlocks => MergeRangeBlocks.Count > 0;

    public bool HasMergeRangeSegments => GetVisibleMergeRangeSegments().Any();

    public bool HasLoadedMergeRangeSegments => _allMergeRangeSegments.Count > 0;

    public bool IsMergeRangeEmptyVisible => !IsMergeRangeDialogBusy && !HasLoadedMergeRangeSegments;

    public bool IsMergeRangeBlockEmptyVisible => !IsMergeRangeDialogBusy && HasLoadedMergeRangeSegments && !HasMergeRangeSegments;

    public bool HasSelectedMergeRangeSegments => _allMergeRangeSegments.Any(segment => segment.IsSelected);

    public bool CanApplyMergeRangeSelection => !IsMergeRangeDialogBusy && HasSelectedMergeRangeSegments;

    public string MergeRangeSelectedCountLabel =>
        AppText.CurrentCultureCode == AppText.EnglishCultureCode
            ? $"{_allMergeRangeSegments.Count(segment => segment.IsSelected):N0} segments selected"
            : $"{_allMergeRangeSegments.Count(segment => segment.IsSelected):N0} 段已选择";

    public bool HasMergeReport => !string.IsNullOrWhiteSpace(MergeReportOutputPath) || !string.IsNullOrWhiteSpace(MergeReportErrorMessage);

    public bool CanGoPreviousMergeStep => MergeStepIndex > 0 && !IsMergeBusy;

    public bool CanCancelMerge => IsMergeBusy;

    public bool CanHideToTray => string.IsNullOrWhiteSpace(TrayHideBlockReason);

    public string TrayHideBlockReason
    {
        get
        {
            if (IsBusy)
            {
                return L("请等待当前字体库任务完成后再隐藏到托盘。");
            }

            if (IsOnlineSearchBusy)
            {
                return L("请等待在线字体搜索完成后再隐藏到托盘。");
            }

            if (_isProcessingOnlineDownloadQueue)
            {
                return L("请等待在线字体下载完成后再隐藏到托盘。");
            }

            if (IsMergeBusy)
            {
                return L("请等待字体合并任务完成或取消后再隐藏到托盘。");
            }

            if (IsMergeRangeDialogBusy)
            {
                return L("请等待合并范围分析完成后再隐藏到托盘。");
            }

            if (IsGlyphLoading)
            {
                return L("请等待字形读取完成后再隐藏到托盘。");
            }

            return "";
        }
    }

    public string MergeNextActionLabel => MergeStepIndex switch
    {
        3 => L("开始导出"),
        4 => L("完成"),
        _ => L("下一步")
    };

    public string MergePreviewSummary
    {
        get
        {
            if (_currentMergePreview is null)
            {
                return L("尚未执行冲突预览。");
            }

            if (AppText.CurrentCultureCode == AppText.EnglishCultureCode)
            {
                var overwriteText = SelectedMergeMode == FontMergeMode.Overwrite || _currentMergePreview.OverwrittenCodePointCount > 0
                    ? $" · overwrite {_currentMergePreview.OverwrittenCodePointCount:N0}"
                    : "";
                return $"Ranges {string.Join(", ", _currentMergePreview.Ranges.Select(range => range.Label))} · {_currentMergePreview.RequestedCodePointCount:N0} code points checked · supplemental coverage {_currentMergePreview.SupplementalCoverageCount:N0} · default merge {_currentMergePreview.MergeCodePointCount:N0}{overwriteText}";
            }

            var zhOverwriteText = SelectedMergeMode == FontMergeMode.Overwrite || _currentMergePreview.OverwrittenCodePointCount > 0
                ? $" · 覆盖 {_currentMergePreview.OverwrittenCodePointCount:N0} 个"
                : "";
            return $"范围 {string.Join(", ", _currentMergePreview.Ranges.Select(range => range.Label))} · 预计检查 {_currentMergePreview.RequestedCodePointCount:N0} 个码位 · 补充字体覆盖 {_currentMergePreview.SupplementalCoverageCount:N0} 个 · 默认合并 {_currentMergePreview.MergeCodePointCount:N0} 个{zhOverwriteText}";
        }
    }

    public string MergeDefaultStrategy => _currentMergePreview?.DefaultConflictStrategy ?? BuildMergeModeStrategy(SelectedMergeMode);

    public string MergeProgressLabel => IsMergeBusy
        ? $"{MergeProgressStage} {MergeProgressPercent}% · {MergeProgressMessage}"
        : MergeStatus;

}
