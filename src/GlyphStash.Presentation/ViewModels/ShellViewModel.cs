using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlyphStash.Application.Abstractions.Fonts;
using GlyphStash.Application.Fonts;
using GlyphStash.Domain.Fonts;
using GlyphStash.Localization;
using GlyphStash.Presentation.Services;
using DomainUnicodeRange = GlyphStash.Domain.Fonts.UnicodeRange;

namespace GlyphStash.Presentation.ViewModels;

public sealed partial class ShellViewModel : ObservableObject
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

    private readonly FontLibraryService _fontLibraryService;
    private readonly LocalFontManagementService? _localManagementService;
    private readonly IUserFileDialogService _fileDialogService;
    private readonly IUserClipboardService _clipboardService;
    private readonly IFontPreviewRegistry _fontPreviewRegistry;
    private readonly IAppLocalizationService? _localizationService;
    private readonly OnlineFontService? _onlineFontService;
    private readonly IGlyphCatalogService? _glyphCatalogService;
    private readonly FontMergeService? _fontMergeService;
    private readonly List<FontFamilyItemViewModel> _allFonts = [];
    private FontImportPreview? _currentImportPreview;
    private bool _isProcessingOnlineDownloadQueue;
    private FontFaceItemViewModel? _currentGlyphFace;
    private FontMergePreview? _currentMergePreview;
    private CancellationTokenSource? _mergeCancellation;
    private readonly List<MergeRangeSegmentItemViewModel> _allMergeRangeSegments = [];
    private bool _isApplyingSettings;
    private bool _isSyncingLocalizedOptions;

    [ObservableProperty]
    private NavigationItemViewModel? _selectedNavigationItem;

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
    private FontFamilyItemViewModel? _selectedFont;

    [ObservableProperty]
    private FontFaceItemViewModel? _selectedPreviewFace;

    [ObservableProperty]
    private bool _isBusy;

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
    private string _managedFontDirectory = "";

    [ObservableProperty]
    private string _googleFontsApiKeyText = "";

    [ObservableProperty]
    private string _importStatus = AppText.TranslateLiteral("请选择字体文件并选择 GlyphStash 管理目录。");

    [ObservableProperty]
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
    private RemoteFontFamilyItemViewModel? _selectedRemoteFont;

    [ObservableProperty]
    private string _onlineStatus = AppText.TranslateLiteral("请在设置页配置 Google Fonts API key 后搜索在线字体。");

    [ObservableProperty]
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
    private string _glyphSearchText = "";

    [ObservableProperty]
    private string _selectedUnicodeBlock = "全部区块";

    [ObservableProperty]
    private GlyphItemViewModel? _selectedGlyph;

    [ObservableProperty]
    private string _glyphStatus = AppText.TranslateLiteral("请从字体详情进入字形浏览。");

    [ObservableProperty]
    private int _glyphPageNumber = 1;

    [ObservableProperty]
    private int _glyphTotalPages = 1;

    [ObservableProperty]
    private int _mergeStepIndex;

    [ObservableProperty]
    private string _mergeBaseSearchText = "";

    [ObservableProperty]
    private string _mergeSupplementalSearchText = "";

    [ObservableProperty]
    private FontFamilyItemViewModel? _selectedMergeBaseFont;

    [ObservableProperty]
    private FontFamilyItemViewModel? _selectedMergeSupplementalFont;

    [ObservableProperty]
    private string _mergeUnicodeRanges = "U+4E00-U+9FFF";

    [ObservableProperty]
    private bool _isMergeRangeDialogOpen;

    [ObservableProperty]
    private bool _isMergeRangeDialogBusy;

    [ObservableProperty]
    private string _mergeRangeDialogStatus = AppText.TranslateLiteral("请选择基础字体和补充字体。");

    [ObservableProperty]
    private string _mergeRangeBaseSummary = AppText.TranslateLiteral("未读取基础字体 A。");

    [ObservableProperty]
    private string _mergeRangeSupplementalSummary = AppText.TranslateLiteral("未读取补充字体 B。");

    [ObservableProperty]
    private MergeRangeBlockItemViewModel? _selectedMergeRangeBlock;

    [ObservableProperty]
    private string _selectedMergeModeLabel = SupplementMergeModeLabel;

    [ObservableProperty]
    private LocalizedOptionViewModel? _selectedMergeModeOption;

    [ObservableProperty]
    private bool _mergeLicenseConfirmed;

    [ObservableProperty]
    private string _mergeOutputFontName = "";

    [ObservableProperty]
    private string _mergeOutputPath = "";

    [ObservableProperty]
    private string _mergeStatus = AppText.TranslateLiteral("请选择基础字体和补充字体。");

    [ObservableProperty]
    private bool _isMergeBusy;

    [ObservableProperty]
    private int _mergeProgressPercent;

    [ObservableProperty]
    private string _mergeProgressStage = "";

    [ObservableProperty]
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

    [ObservableProperty]
    private LanguageOptionViewModel? _selectedLanguage;

    public ShellViewModel(FontLibraryService fontLibraryService)
        : this(fontLibraryService, null, NullUserFileDialogService.Instance)
    {
    }

    public ShellViewModel(
        FontLibraryService fontLibraryService,
        LocalFontManagementService? localManagementService,
        IUserFileDialogService fileDialogService,
        OnlineFontService? onlineFontService = null,
        IGlyphCatalogService? glyphCatalogService = null,
        IUserClipboardService? clipboardService = null,
        IFontPreviewRegistry? fontPreviewRegistry = null,
        FontMergeService? fontMergeService = null,
        IAppLocalizationService? localizationService = null)
    {
        _fontLibraryService = fontLibraryService;
        _localManagementService = localManagementService;
        _fileDialogService = fileDialogService;
        _onlineFontService = onlineFontService;
        _glyphCatalogService = glyphCatalogService;
        _fontMergeService = fontMergeService;
        _clipboardService = clipboardService ?? NullUserFileDialogService.Instance;
        _fontPreviewRegistry = fontPreviewRegistry ?? NullFontPreviewRegistry.Instance;
        _localizationService = localizationService;
        InitializeLocalizedOptions();
        if (_localizationService is not null)
        {
            foreach (var language in _localizationService.SupportedLanguages)
            {
                LanguageOptions.Add(new LanguageOptionViewModel(language));
            }

            _isApplyingSettings = true;
            try
            {
                SelectedLanguage = LanguageOptions.FirstOrDefault(language =>
                    string.Equals(language.CultureCode, _localizationService.CurrentCulture.Name, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                _isApplyingSettings = false;
            }

            _localizationService.CultureChanged += (_, _) => RefreshLocalizedState();
        }
        RefreshMergeStepState();
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

    public ObservableCollection<MergeRangeBlockItemViewModel> MergeRangeBlocks { get; } = [];

    public ObservableCollection<MergeRangeSegmentItemViewModel> MergeRangeSegments { get; } = [];

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

    public bool HasMergeRangeSegments => MergeRangeSegments.Count > 0;

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

    [RelayCommand]
    public async Task InitializeAsync()
    {
        SelectedNavigationItem ??= NavigationItems.First(item => item.Key == "font-library");
        IsBusy = true;
        ScanStatus = L("正在读取 SQLite 字体索引...");
        try
        {
            if (_localManagementService is not null)
            {
                var settings = await _localManagementService.GetSettingsAsync(CancellationToken.None);
                if (_localizationService is not null && !string.IsNullOrWhiteSpace(settings?.UiCultureCode))
                {
                    _isApplyingSettings = true;
                    try
                    {
                        _localizationService.SetCulture(settings.UiCultureCode);
                        SelectedLanguage = LanguageOptions.FirstOrDefault(language =>
                            string.Equals(language.CultureCode, _localizationService.CurrentCulture.Name, StringComparison.OrdinalIgnoreCase));
                    }
                    finally
                    {
                        _isApplyingSettings = false;
                    }
                }

                ManagedFontDirectory = settings?.ManagedFontDirectory ?? "";
                GoogleFontsApiKeyText = settings?.GoogleFontsApiKey ?? "";
            }

            var cached = await _fontLibraryService.LoadCachedFontsAsync(new FontSearchQuery(), CancellationToken.None);
            if (cached.Count == 0)
            {
                ScanStatus = L("首次启动：正在扫描 Windows 字体目录...");
                cached = await _fontLibraryService.RescanAsync(CancellationToken.None);
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
            var message = BuildErrorMessage(ex);
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
            var fonts = await _fontLibraryService.RescanAsync(CancellationToken.None);
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
            var message = BuildErrorMessage(ex);
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

        if (_glyphCatalogService is null)
        {
            MergeRangeDialogStatus = L("字形覆盖服务未装配。");
            return;
        }

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
                CancellationToken.None);
            var supplementalCoverage = await _glyphCatalogService.GetCoverageAsync(
                new GlyphCoverageQuery(supplementalFace.FilePath, supplementalFace.StyleLabel),
                CancellationToken.None);

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
            MergeRangeDialogStatus = BuildErrorMessage(ex);
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
        if (_fontMergeService is null)
        {
            MergeStatus = L("合并服务不可用。");
            return;
        }

        IsMergeBusy = true;
        MergeProgressPercent = 0;
        MergeProgressStage = L("预览");
        MergeProgressMessage = L("正在预检查输入。");
        try
        {
            var preview = await _fontMergeService.PreviewAsync(CreateMergeRequest(includeOutput: false), CancellationToken.None);
            ApplyMergePreview(preview);
            MergeStatus = preview.HasBlockingIssues
                ? L("冲突预览存在阻止级问题，请检查提示。")
                : L("冲突预览完成，可继续授权与导出。");
        }
        catch (Exception ex)
        {
            MergeStatus = BuildErrorMessage(ex);
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
        var path = await _fileDialogService.PickMergeOutputFileAsync(suggested, CancellationToken.None);
        if (!string.IsNullOrWhiteSpace(path))
        {
            MergeOutputPath = path;
        }
    }

    [RelayCommand]
    private async Task StartMergeAsync()
    {
        if (_fontMergeService is null)
        {
            MergeStatus = L("合并服务不可用。");
            return;
        }

        _mergeCancellation?.Dispose();
        _mergeCancellation = new CancellationTokenSource();
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
            MergeStatus = result.Summary;
            ShowToast(result.Succeeded ? L("合并导出完成") : L("合并导出失败"));
        }
        catch (Exception ex)
        {
            MergeStatus = BuildErrorMessage(ex);
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
        if (_fontMergeService is null)
        {
            MergeStatus = L("合并服务不可用。");
            return;
        }

        var path = await _fileDialogService.PickMergeReportFileAsync(CancellationToken.None);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var report = await _fontMergeService.ReadReportAsync(path, CancellationToken.None);
            ApplyMergeReport(report, path);
            MergeStepIndex = 4;
            MergeStatus = L("历史合并报告已加载。");
        }
        catch (Exception ex)
        {
            MergeStatus = BuildErrorMessage(ex);
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
        if (_localManagementService is not null)
        {
            await _localManagementService.SetFavoriteAsync(font.FamilyName, font.IsFavorite, CancellationToken.None);
        }

        ShowToast(font.IsFavorite ? L("已加入收藏") : L("已取消收藏"));
    }

    [RelayCommand]
    private async Task OpenImportDialogAsync()
    {
        IsImportDialogOpen = true;
        ImportStatus = HasManagedFontDirectory ? L("请选择字体文件。") : L("导入前必须先选择 GlyphStash 管理目录。");
        ImportPreviewItems.Clear();
        OnPropertyChanged(nameof(HasImportPreview));

        var files = await _fileDialogService.PickFontFilesAsync(CancellationToken.None);
        if (files.Count == 0 || _localManagementService is null)
        {
            return;
        }

        try
        {
            _currentImportPreview = await _localManagementService.PreviewImportAsync(files, CancellationToken.None);
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
            ImportStatus = BuildErrorMessage(ex);
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
        var directory = await _fileDialogService.PickManagedDirectoryAsync(CancellationToken.None);
        if (string.IsNullOrWhiteSpace(directory) || _localManagementService is null)
        {
            return;
        }

        await _localManagementService.SaveManagedDirectoryAsync(directory, CancellationToken.None);
        ManagedFontDirectory = directory;
        ImportStatus = L("管理目录已选择，可以开始导入。");
        await ReloadOperationLogsAsync();
        ShowToast(L("管理目录已更新"));
    }

    [RelayCommand]
    private async Task SaveGoogleFontsApiKeyAsync()
    {
        if (_localManagementService is null)
        {
            return;
        }

        await _localManagementService.SaveGoogleFontsApiKeyAsync(GoogleFontsApiKeyText, CancellationToken.None);
        OnlineStatus = string.IsNullOrWhiteSpace(GoogleFontsApiKeyText)
            ? L("Google Fonts API key 已清空。")
            : L("Google Fonts API key 已保存，可以搜索在线字体。");
        await ReloadOperationLogsAsync();
        ShowToast(L("Google Fonts API key 已保存"));
    }

    [RelayCommand]
    private async Task SearchOnlineFontsAsync()
    {
        if (_onlineFontService is null)
        {
            OnlineStatus = L("在线字体服务未装配。");
            return;
        }

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
                CancellationToken.None);
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
            OnlineStatus = BuildErrorMessage(ex);
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
        if (_onlineFontService is null || SelectedRemoteFont is null)
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
        if (_onlineFontService is null || _isProcessingOnlineDownloadQueue)
        {
            return;
        }

        _isProcessingOnlineDownloadQueue = true;
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
                    var result = await _onlineFontService.DownloadAsync(item.Family, item.Styles, item.Options, CancellationToken.None);
                    item.MarkSucceeded(result.Message);
                    var cached = await _fontLibraryService.LoadCachedFontsAsync(new FontSearchQuery(), CancellationToken.None);
                    ReplaceFonts(cached);
                    await ReloadM2StateAsync();
                    OnlineStatus = AppText.CurrentCultureCode == AppText.EnglishCultureCode ? $"Download complete: {item.FamilyName}" : $"下载完成：{item.FamilyName}";
                    ShowToast(AppText.CurrentCultureCode == AppText.EnglishCultureCode ? $"Downloaded {item.FamilyName}" : $"已下载 {item.FamilyName}");
                }
                catch (Exception ex)
                {
                    var message = BuildErrorMessage(ex);
                    item.MarkFailed(message);
                    OnlineStatus = AppText.CurrentCultureCode == AppText.EnglishCultureCode ? $"Download failed: {item.FamilyName}. {message}" : $"下载失败：{item.FamilyName}。{message}";
                    ShowToast(L("下载失败，可重试"));
                }
            }
        }
        finally
        {
            _isProcessingOnlineDownloadQueue = false;
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

        if (_glyphCatalogService is null)
        {
            GlyphStatus = L("字形浏览服务未装配。");
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
        await LoadGlyphsAsync();
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
        await LoadGlyphsAsync();
    }

    [RelayCommand]
    private async Task PreviousGlyphPageAsync()
    {
        if (GlyphPageNumber <= 1)
        {
            return;
        }

        GlyphPageNumber--;
        await LoadGlyphsAsync();
    }

    [RelayCommand]
    private async Task CopySelectedGlyphCharacterAsync()
    {
        if (SelectedGlyph is null)
        {
            return;
        }

        await _clipboardService.SetTextAsync(SelectedGlyph.Character, CancellationToken.None);
        ShowToast(AppText.CurrentCultureCode == AppText.EnglishCultureCode ? $"Copied character: {SelectedGlyph.Character}" : $"已复制字符：{SelectedGlyph.Character}");
    }

    [RelayCommand]
    private async Task CopySelectedGlyphUnicodeAsync()
    {
        if (SelectedGlyph is null)
        {
            return;
        }

        await _clipboardService.SetTextAsync(SelectedGlyph.UnicodeLabel, CancellationToken.None);
        ShowToast(AppText.CurrentCultureCode == AppText.EnglishCultureCode ? $"Copied Unicode: {SelectedGlyph.UnicodeLabel}" : $"已复制 Unicode：{SelectedGlyph.UnicodeLabel}");
    }

    [RelayCommand]
    private async Task StartImportAsync()
    {
        if (_localManagementService is null || _currentImportPreview is null)
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
                CancellationToken.None);
            var cached = await _fontLibraryService.LoadCachedFontsAsync(new FontSearchQuery(), CancellationToken.None);
            ReplaceFonts(cached);
            await ReloadM2StateAsync();
            IsImportDialogOpen = false;
            ShowToast(AppText.CurrentCultureCode == AppText.EnglishCultureCode
                ? $"Import complete: {result.ImportedCount:N0} succeeded, {result.FailedCount:N0} failed"
                : $"导入完成：{result.ImportedCount} 个成功，{result.FailedCount} 个失败");
        }
        catch (Exception ex)
        {
            ImportStatus = BuildErrorMessage(ex);
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
        if (_localManagementService is not null)
        {
            await _localManagementService.SetTagsAsync(SelectedFont.FamilyName, tags, CancellationToken.None);
            await _localManagementService.SetCollectionsAsync(SelectedFont.FamilyName, collections, CancellationToken.None);
        }

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
        if (_localManagementService is null || string.IsNullOrWhiteSpace(PendingDeleteTagName))
        {
            CloseDeleteTagDialog();
            return;
        }

        var deletedName = PendingDeleteTagName;
        await _localManagementService.DeleteTagAsync(deletedName, CancellationToken.None);
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
        if (SelectedFont is null || _localManagementService is null)
        {
            IsUninstallDialogOpen = false;
            return;
        }

        var result = await _localManagementService.UninstallManagedFontAsync(SelectedFont.ToRecord(), CancellationToken.None);
        IsUninstallDialogOpen = false;
        ShowToast(result.Message);
        await ReloadOperationLogsAsync();
    }

    [RelayCommand]
    private async Task InstallSelectedFontAsync()
    {
        if (SelectedFont is null || _localManagementService is null)
        {
            return;
        }

        var result = await _localManagementService.InstallFontAsync(SelectedFont.ToRecord(), CancellationToken.None);
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
        if (font is null || _localManagementService is null)
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
                ? await _localManagementService.DeactivateFontAsync(owner, font.ToRecord(), CancellationToken.None)
                : await _localManagementService.ActivateFontAsync(owner, font.ToRecord(), CancellationToken.None);

            if (result.Succeeded)
            {
                font.SetActivationState(wasTemporarilyEnabled ? FontActivationState.NotEnabled : FontActivationState.TemporarilyEnabled);
            }

            ShowToast(result.Message);
            await ReloadM2StateAsync();
        }
        catch (Exception ex)
        {
            ShowToast(BuildErrorMessage(ex));
        }
    }

    [RelayCommand]
    private async Task CreateCollectionAsync()
    {
        if (_localManagementService is null || string.IsNullOrWhiteSpace(NewCollectionName))
        {
            return;
        }

        await _localManagementService.CreateCollectionAsync(NewCollectionName, CancellationToken.None);
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
        if (SelectedCollection is null || _localManagementService is null)
        {
            IsDeleteCollectionDialogOpen = false;
            return;
        }

        var deletedName = SelectedCollection.Name;
        await _localManagementService.DeleteCollectionAsync(deletedName, CancellationToken.None);
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
        if (font is null || SelectedCollection is null || _localManagementService is null)
        {
            return;
        }

        await _localManagementService.RemoveFontFromCollectionAsync(SelectedCollection.Name, font.FamilyName, CancellationToken.None);
        await ReloadCollectionsAsync();
        ShowToast(L("已从集合移除字体"));
    }

    [RelayCommand]
    private async Task ActivateSelectedCollectionAsync()
    {
        if (SelectedCollection is null || _localManagementService is null)
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
                    CancellationToken.None);
                skippedCount++;
                continue;
            }

            var result = await _localManagementService.ActivateFontAsync($"collection:{SelectedCollection.Name}", font.ToRecord(), CancellationToken.None);
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
        if (SelectedCollection is null || _localManagementService is null)
        {
            return;
        }

        foreach (var font in CollectionFonts)
        {
            await _localManagementService.DeactivateFontAsync($"collection:{SelectedCollection.Name}", font.ToRecord(), CancellationToken.None);
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
        if (value is null || _localizationService is null)
        {
            return;
        }

        _localizationService.SetCulture(value.CultureCode);
        if (_isApplyingSettings)
        {
            return;
        }

        _ = SaveSelectedLanguageAsync(value);
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
        OnPropertyChanged(nameof(CurrentPageTitle));
        OnPropertyChanged(nameof(CurrentPageDescription));
        OnPropertyChanged(nameof(CurrentPageMilestone));
    }

    partial void OnIsGlyphBrowserOpenChanged(bool value) => NotifyNavigationState();

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
            _ = LoadGlyphsAsync();
        }
    }

    partial void OnSelectedUnicodeBlockChanged(string value)
    {
        if (IsGlyphBrowserOpen)
        {
            GlyphPageNumber = 1;
            _ = LoadGlyphsAsync();
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

    partial void OnIsMergeBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanGoPreviousMergeStep));
        OnPropertyChanged(nameof(CanCancelMerge));
        OnPropertyChanged(nameof(MergeProgressLabel));
    }

    partial void OnIsMergeRangeDialogBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsMergeRangeEmptyVisible));
        OnPropertyChanged(nameof(IsMergeRangeBlockEmptyVisible));
        OnPropertyChanged(nameof(CanApplyMergeRangeSelection));
    }

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

    private async Task SaveSelectedLanguageAsync(LanguageOptionViewModel language)
    {
        try
        {
            if (_localManagementService is not null)
            {
                await _localManagementService.SaveUiCultureAsync(language.CultureCode, CancellationToken.None);
                await ReloadOperationLogsAsync();
            }

            ShowToast(T("Toast.LanguageChangedFormat", language.DisplayName));
        }
        catch (Exception ex)
        {
            ShowToast(BuildErrorMessage(ex));
        }
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

    private static string L(string text) => AppText.TranslateLiteral(text);

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
        SelectSourceFilterOption(SelectedSourceFilter);
        SelectStateFilterOption(SelectedStateFilter);
        SelectTagFilterOption(SelectedTagFilter);
        SelectCollectionFilterOption(SelectedCollectionFilter);
        SelectOnlineSubsetOption(SelectedOnlineSubset);
        SelectOnlineCategoryOption(SelectedOnlineCategory);
        SelectMergeModeOption(SelectedMergeModeLabel);
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

    private void SelectSourceFilterOption(string value) => SelectOption(SourceFilterOptions, value, option => SelectedSourceFilterOption = option);

    private void SelectStateFilterOption(string value) => SelectOption(StateFilterOptions, value, option => SelectedStateFilterOption = option);

    private void SelectTagFilterOption(string value) => SelectOption(TagFilterOptions, value, option => SelectedTagFilterOption = option);

    private void SelectCollectionFilterOption(string value) => SelectOption(CollectionFilterOptions, value, option => SelectedCollectionFilterOption = option);

    private void SelectOnlineSubsetOption(string value) => SelectOption(OnlineSubsetOptionModels, value, option => SelectedOnlineSubsetOption = option);

    private void SelectOnlineCategoryOption(string value) => SelectOption(OnlineCategoryOptionModels, value, option => SelectedOnlineCategoryOption = option);

    private void SelectMergeModeOption(string value) => SelectOption(MergeModeOptionModels, value, option => SelectedMergeModeOption = option);

    private void SelectOption(
        IReadOnlyList<LocalizedOptionViewModel> options,
        string value,
        Action<LocalizedOptionViewModel?> select)
    {
        _isSyncingLocalizedOptions = true;
        try
        {
            select(options.FirstOrDefault(option => string.Equals(option.Value, value, StringComparison.CurrentCultureIgnoreCase))
                   ?? options.FirstOrDefault());
        }
        finally
        {
            _isSyncingLocalizedOptions = false;
        }
    }

    private void SetStableFilterFromOption(
        LocalizedOptionViewModel? option,
        string fallback,
        Action<string> select)
    {
        _isSyncingLocalizedOptions = true;
        try
        {
            select(option?.Value ?? fallback);
        }
        finally
        {
            _isSyncingLocalizedOptions = false;
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

    private async Task LoadGlyphsAsync()
    {
        if (_glyphCatalogService is null || !IsGlyphBrowserOpen)
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
                CancellationToken.None);

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
        catch (Exception ex)
        {
            GlyphStatus = BuildErrorMessage(ex);
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
        if (_localManagementService is null)
        {
            return;
        }

        _ = ReloadCollectionsAsync();
    }

    private async Task ReloadM2StateAsync(string? preferredTagFilter = null, string? preferredCollectionFilter = null)
    {
        await ReloadTagsAsync(preferredTagFilter);
        await ReloadCollectionsAsync(preferredCollectionFilter);
        await ReloadOperationLogsAsync();
    }

    private async Task ReloadTagsAsync(string? preferredFilter = null)
    {
        if (_localManagementService is null)
        {
            return;
        }

        var selectedTag = NormalizeFilter(preferredFilter ?? SelectedTagFilter, AllTagFilter);
        var tags = await _localManagementService.GetTagsAsync(CancellationToken.None);
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

    private async Task ReloadCollectionsAsync(string? preferredFilter = null)
    {
        if (_localManagementService is null)
        {
            return;
        }

        var selectedName = SelectedCollection?.Name;
        var collections = await _localManagementService.GetCollectionsAsync(CancellationToken.None);
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

    private async Task ReloadOperationLogsAsync()
    {
        if (_localManagementService is null)
        {
            return;
        }

        OperationLogs.Clear();
        var logs = await _localManagementService.GetRecentOperationsAsync(8, CancellationToken.None);
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
        MergeRangeSegments.Clear();
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
        MergeRangeSegments.Clear();

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
        MergeRangeSegments.Clear();
        if (SelectedMergeRangeBlock is null)
        {
            NotifyMergeRangeDialogState();
            return;
        }

        foreach (var segment in _allMergeRangeSegments.Where(segment =>
                     SelectedMergeRangeBlock.IsAll
                     || string.Equals(segment.BlockName, SelectedMergeRangeBlock.Name, StringComparison.Ordinal)))
        {
            MergeRangeSegments.Add(segment);
        }

        NotifyMergeRangeDialogState();
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

        OnPropertyChanged(nameof(IsMergeStepSelectFonts));
        OnPropertyChanged(nameof(IsMergeStepRanges));
        OnPropertyChanged(nameof(IsMergeStepPreview));
        OnPropertyChanged(nameof(IsMergeStepExport));
        OnPropertyChanged(nameof(IsMergeStepReport));
        OnPropertyChanged(nameof(CanGoPreviousMergeStep));
        OnPropertyChanged(nameof(MergeNextActionLabel));
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
            SelectTagFilterOption(filter);
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
            SelectCollectionFilterOption(filter);
            OnPropertyChanged(nameof(SelectedCollectionFilter));
            return;
        }

        SelectedCollectionFilter = filter;
    }

    private static string BuildErrorMessage(Exception exception)
    {
        for (var candidate = exception; candidate is not null; candidate = candidate.InnerException)
        {
            if (candidate is InvalidOperationException && !string.IsNullOrWhiteSpace(candidate.Message))
            {
                return candidate.Message;
            }
        }

        var current = exception;
        while (current.InnerException is not null)
        {
            current = current.InnerException;
        }

        return string.IsNullOrWhiteSpace(current.Message) ? current.GetType().Name : current.Message;
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
