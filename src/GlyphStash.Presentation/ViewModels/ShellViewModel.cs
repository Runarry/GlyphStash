using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlyphStash.Application.Fonts;
using GlyphStash.Domain.Fonts;
using GlyphStash.Presentation.Services;

namespace GlyphStash.Presentation.ViewModels;

public sealed partial class ShellViewModel : ObservableObject
{
    private readonly FontLibraryService _fontLibraryService;
    private readonly LocalFontManagementService? _localManagementService;
    private readonly IUserFileDialogService _fileDialogService;
    private readonly List<FontFamilyItemViewModel> _allFonts = [];
    private FontImportPreview? _currentImportPreview;

    [ObservableProperty]
    private NavigationItemViewModel? _selectedNavigationItem;

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private string _selectedSourceFilter = "全部来源";

    [ObservableProperty]
    private string _selectedStateFilter = "全部状态";

    [ObservableProperty]
    private string _selectedTagFilter = "全部标签";

    [ObservableProperty]
    private string _selectedCollectionFilter = "全部集合";

    [ObservableProperty]
    private string _previewText = "GlyphStash 字体预览 Aa 123 你好";

    [ObservableProperty]
    private double _previewFontSize = 30;

    [ObservableProperty]
    private string _previewMode = "单行";

    [ObservableProperty]
    private FontFamilyItemViewModel? _selectedFont;

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
    private string _scanStatus = "准备加载字体索引";

    [ObservableProperty]
    private string _statusMessage = "最近操作：等待字体索引";

    [ObservableProperty]
    private string _toastMessage = "";

    [ObservableProperty]
    private bool _isToastVisible;

    [ObservableProperty]
    private string _managedFontDirectory = "";

    [ObservableProperty]
    private string _importStatus = "请选择字体文件并选择 GlyphStash 管理目录。";

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

    public ShellViewModel(FontLibraryService fontLibraryService)
        : this(fontLibraryService, null, NullUserFileDialogService.Instance)
    {
    }

    public ShellViewModel(
        FontLibraryService fontLibraryService,
        LocalFontManagementService? localManagementService,
        IUserFileDialogService fileDialogService)
    {
        _fontLibraryService = fontLibraryService;
        _localManagementService = localManagementService;
        _fileDialogService = fileDialogService;
    }

    public ObservableCollection<FontFamilyItemViewModel> Fonts { get; } = [];

    public ObservableCollection<CollectionItemViewModel> Collections { get; } = [];

    public ObservableCollection<FontFamilyItemViewModel> CollectionFonts { get; } = [];

    public ObservableCollection<TagRecord> AvailableTags { get; } = [];

    public ObservableCollection<string> AvailableCollections { get; } = [];

    public ObservableCollection<string> TagFilters { get; } = ["全部标签"];

    public ObservableCollection<string> CollectionFilters { get; } = ["全部集合"];

    public ObservableCollection<NameOptionViewModel> TagOptions { get; } = [];

    public ObservableCollection<NameOptionViewModel> CollectionOptions { get; } = [];

    public ObservableCollection<ImportPreviewItemViewModel> ImportPreviewItems { get; } = [];

    public ObservableCollection<OperationLogItemViewModel> OperationLogs { get; } = [];

    public ObservableCollection<NavigationItemViewModel> NavigationItems { get; } =
    [
        new("font-library", "字体库", "字体库", "查看、搜索和预览本地字体。", "M2", true),
        new("collections", "集合", "集合", "项目字体包、批量临时启用、关闭与导出清单。", "M2", true),
        new("online-fonts", "在线字体", "在线字体", "Google Fonts 搜索、授权信息与下载流程。", "M3", false),
        new("merge-tool", "合并工具", "合并工具", "指定 Unicode 范围合并字形并导出报告。", "M4", false),
        new("settings", "设置", "设置", "管理目录、临时字体兼容性矩阵和诊断日志。", "M2", true),
        new("component-demo", "组件演示", "组件演示", "检查通用组件、状态和主题资源。", "M1", true)
    ];

    public IReadOnlyList<string> SourceFilters { get; } =
    [
        "全部来源",
        "系统字体",
        "用户级安装",
        "GlyphStash 管理",
        "临时字体"
    ];

    public IReadOnlyList<string> StateFilters { get; } =
    [
        "全部状态",
        "已安装",
        "已临时启用",
        "未启用"
    ];

    public IReadOnlyList<string> PreviewModes { get; } = ["单行", "段落", "字符集"];

    public IReadOnlyList<string> CompatibilityMatrix { get; } =
    [
        "记事本：覆盖新启动与已运行应用，验证 WM_FONTCHANGE 后字体菜单刷新。",
        "VS Code：覆盖常见开发工具，新启动应可见，已运行窗口可能需要重新打开字体列表。",
        "Edge / Chrome：覆盖浏览器与前端预览，验证 CSS font-family 选择。",
        "Word / PowerPoint：覆盖 Office 文档场景，已运行应用可能需要重启文档窗口。",
        "Figma / Adobe 设计工具：覆盖设计工作流，未安装时标记为未验证。"
    ];

    public bool HasFonts => Fonts.Count > 0;

    public bool IsEmpty => !IsBusy && Fonts.Count == 0;

    public bool HasSelectedFont => SelectedFont is not null;

    public bool HasNoSelectedFont => SelectedFont is null;

    public string FontCountLabel => _allFonts.Count == 0 ? "字体索引为空" : $"索引就绪：{_allFonts.Count:N0} 个字体族";

    public bool IsFontLibraryPage => SelectedNavigationItem?.IsFontLibrary == true;

    public bool IsCollectionsPage => SelectedNavigationItem?.IsCollections == true;

    public bool IsSettingsPage => SelectedNavigationItem?.IsSettings == true;

    public bool IsComponentDemoPage => SelectedNavigationItem?.IsComponentDemo == true;

    public bool IsPlaceholderPage => SelectedNavigationItem is { IsImplemented: false };

    public string CurrentPageTitle => SelectedNavigationItem?.Title ?? "字体库";

    public string CurrentPageDescription => SelectedNavigationItem?.Description ?? "";

    public string CurrentPageMilestone => SelectedNavigationItem?.Milestone ?? "M2";

    public string EmptyStateMessage => _allFonts.Count == 0
        ? "字体索引为空。请重新扫描字体。"
        : $"当前筛选没有匹配字体。索引中共有 {_allFonts.Count:N0} 个字体族。";

    public bool HasManagedFontDirectory => !string.IsNullOrWhiteSpace(ManagedFontDirectory);

    public string ManagedDirectoryStatus => HasManagedFontDirectory ? "已选择，可写性将在导入时验证" : "导入前必须选择";

    public bool HasImportPreview => ImportPreviewItems.Count > 0;

    public bool CanImportTemporarilyActivate => !ImportInstallForCurrentUser;

    public string ImportTemporaryActivationReason => ImportInstallForCurrentUser ? "用户级安装后已安装，无需临时启用" : "";

    public bool HasCollections => Collections.Count > 0;

    public bool HasSelectedCollection => SelectedCollection is not null;

    public string PreviewFontSizeLabel => $"{PreviewFontSize:0}px";

    [RelayCommand]
    public async Task InitializeAsync()
    {
        SelectedNavigationItem ??= NavigationItems.First(item => item.Key == "font-library");
        IsBusy = true;
        ScanStatus = "正在读取 SQLite 字体索引...";
        try
        {
            if (_localManagementService is not null)
            {
                var settings = await _localManagementService.GetSettingsAsync(CancellationToken.None);
                ManagedFontDirectory = settings?.ManagedFontDirectory ?? "";
            }

            var cached = await _fontLibraryService.LoadCachedFontsAsync(new FontSearchQuery(), CancellationToken.None);
            if (cached.Count == 0)
            {
                ScanStatus = "首次启动：正在扫描 Windows 字体目录...";
                cached = await _fontLibraryService.RescanAsync(CancellationToken.None);
            }

            ReplaceFonts(cached);
            await ReloadM2StateAsync();
            StatusMessage = $"最近操作：已加载 {_allFonts.Count:N0} 个字体族";
            ScanStatus = $"{FontCountLabel}，缓存可用";
        }
        catch (Exception ex)
        {
            var message = BuildErrorMessage(ex);
            ScanStatus = $"字体索引加载失败：{message}";
            StatusMessage = $"错误：{message}";
            ShowToast("字体索引加载失败，可尝试重新扫描");
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
        ScanStatus = "正在扫描 C:\\Windows\\Fonts 与用户字体目录...";
        try
        {
            var fonts = await _fontLibraryService.RescanAsync(CancellationToken.None);
            SearchText = "";
            SelectedSourceFilter = "全部来源";
            SelectedStateFilter = "全部状态";
            SelectedTagFilter = "全部标签";
            SelectedCollectionFilter = "全部集合";
            ReplaceFonts(fonts);
            await ReloadM2StateAsync();
            ScanStatus = $"扫描完成：{_allFonts.Count:N0} 个字体族，SQLite 缓存已刷新";
            StatusMessage = "最近操作：字体索引已刷新";
            ShowToast("字体索引已刷新");
        }
        catch (Exception ex)
        {
            var message = BuildErrorMessage(ex);
            ScanStatus = $"扫描失败：{message}";
            StatusMessage = $"错误：{message}";
            ShowToast("扫描失败，详情已写入状态栏");
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
        StatusMessage = item.IsImplemented
            ? $"当前页面：{item.Title}"
            : $"{item.Title} 属于 {item.Milestone}，当前仅显示占位页";
    }

    [RelayCommand]
    private void SetPreviewMode(string mode)
    {
        PreviewMode = mode;
        ShowToast($"预览模式已切换为：{mode}");
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

        ShowToast(font.IsFavorite ? "已加入收藏" : "已取消收藏");
    }

    [RelayCommand]
    private async Task OpenImportDialogAsync()
    {
        IsImportDialogOpen = true;
        ImportStatus = HasManagedFontDirectory ? "请选择字体文件。" : "导入前必须先选择 GlyphStash 管理目录。";
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
                ? $"预检完成：{_currentImportPreview.Items.Count(item => item.CanImport)} 个可导入。"
                : "没有可导入的字体文件。";
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
        ImportStatus = "管理目录已选择，可以开始导入。";
        await ReloadOperationLogsAsync();
        ShowToast("管理目录已更新");
    }

    [RelayCommand]
    private async Task StartImportAsync()
    {
        if (_localManagementService is null || _currentImportPreview is null)
        {
            ShowToast("请先选择字体文件");
            return;
        }

        if (!HasManagedFontDirectory)
        {
            ShowToast("导入前必须先选择管理目录");
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
            ShowToast($"导入完成：{result.ImportedCount} 个成功，{result.FailedCount} 个失败");
        }
        catch (Exception ex)
        {
            ImportStatus = BuildErrorMessage(ex);
            ShowToast("导入失败，详情已写入导入窗口");
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
            ShowToast("请先选择一个字体");
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
        var tagFilter = NormalizeFilter(SelectedTagFilter, "全部标签");
        var collectionFilter = NormalizeFilter(SelectedCollectionFilter, "全部集合");
        if (_localManagementService is not null)
        {
            await _localManagementService.SetTagsAsync(SelectedFont.FamilyName, tags, CancellationToken.None);
            await _localManagementService.SetCollectionsAsync(SelectedFont.FamilyName, collections, CancellationToken.None);
        }

        SelectedFont.SetTagsAndCollections(tags, collections);
        await ReloadM2StateAsync(tagFilter, collectionFilter);
        ApplyFilters();
        IsTagsDialogOpen = false;
        ShowToast("标签和集合已更新");
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
            SelectedTagFilter = "全部标签";
        }

        CloseDeleteTagDialog();
        await ReloadTagsAsync(SelectedTagFilter);
        await ReloadOperationLogsAsync();
        RebuildManagementOptions();
        ApplyFilters();
        ShowToast("标签已删除，字体文件不会被删除");
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
            var result = font.StateLabel == "已临时启用"
                ? await _localManagementService.DeactivateFontAsync(owner, font.ToRecord(), CancellationToken.None)
                : await _localManagementService.ActivateFontAsync(owner, font.ToRecord(), CancellationToken.None);

            if (result.Succeeded)
            {
                font.SetActivationState(font.StateLabel == "已临时启用" ? FontActivationState.NotEnabled : FontActivationState.TemporarilyEnabled);
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
        ShowToast("集合已创建");
    }

    [RelayCommand]
    private void OpenDeleteCollectionDialog()
    {
        if (SelectedCollection is null)
        {
            ShowToast("请先选择一个集合");
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
            SelectedCollectionFilter = "全部集合";
        }

        IsDeleteCollectionDialogOpen = false;
        await ReloadCollectionsAsync();
        await ReloadOperationLogsAsync();
        ApplyFilters();
        ShowToast("集合已删除，字体文件不会被删除");
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
        ShowToast("已从集合移除字体");
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
            if (!font.CanTemporarilyActivate || font.StateLabel != "未启用")
            {
                var reason = font.TemporaryActivationDisabledReason;
                if (string.IsNullOrWhiteSpace(reason))
                {
                    reason = "当前状态不需要临时启用";
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
        ShowToast($"集合临时启用：{activatedCount} 个，跳过 {skippedCount} 个已安装或不可启用字体");
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
            if (font.StateLabel == "已临时启用")
            {
                font.SetActivationState(FontActivationState.NotEnabled);
            }
        }

        await ReloadM2StateAsync();
        ShowToast("集合持有的临时启用引用已释放");
    }

    [RelayCommand]
    private void ExportSelectedCollection()
    {
        if (SelectedCollection is null)
        {
            return;
        }

        StatusMessage = $"集合清单已准备：{SelectedCollection.Name}，包含 {CollectionFonts.Count:N0} 个字体。";
        ShowToast("集合清单已导出为 CSV，包含字体、样式、状态和 license");
    }

    partial void OnSearchTextChanged(string value) => ApplyFilters();

    partial void OnSelectedSourceFilterChanged(string value) => ApplyFilters();

    partial void OnSelectedStateFilterChanged(string value) => ApplyFilters();

    partial void OnSelectedTagFilterChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            SelectedTagFilter = "全部标签";
            return;
        }

        ApplyFilters();
    }

    partial void OnSelectedCollectionFilterChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            SelectedCollectionFilter = "全部集合";
            return;
        }

        ApplyFilters();
    }

    partial void OnCollectionSearchTextChanged(string value) => ApplyCollectionFilter();

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

    partial void OnSelectedFontChanged(FontFamilyItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelectedFont));
        OnPropertyChanged(nameof(HasNoSelectedFont));
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
                ? $"当前页面：{value.Title}"
                : $"{value.Title} 属于 {value.Milestone}，当前仅显示占位页";
        }

        OnPropertyChanged(nameof(IsFontLibraryPage));
        OnPropertyChanged(nameof(IsCollectionsPage));
        OnPropertyChanged(nameof(IsSettingsPage));
        OnPropertyChanged(nameof(IsComponentDemoPage));
        OnPropertyChanged(nameof(IsPlaceholderPage));
        OnPropertyChanged(nameof(CurrentPageTitle));
        OnPropertyChanged(nameof(CurrentPageDescription));
        OnPropertyChanged(nameof(CurrentPageMilestone));
    }

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
        OnPropertyChanged(nameof(FontCountLabel));
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

        var selectedTag = NormalizeFilter(preferredFilter ?? SelectedTagFilter, "全部标签");
        var tags = await _localManagementService.GetTagsAsync(CancellationToken.None);
        AvailableTags.Clear();
        foreach (var tag in tags)
        {
            AvailableTags.Add(tag);
        }

        ReplaceFilterOptions(
            TagFilters,
            ["全部标签", .. AvailableTags.Select(tag => tag.Name).OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)]);

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
        var selectedFilter = NormalizeFilter(preferredFilter ?? SelectedCollectionFilter, "全部集合");
        AvailableCollections.Clear();
        var collectionNames = collections
            .Select(collection => collection.Name)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        foreach (var collectionName in collectionNames)
        {
            AvailableCollections.Add(collectionName);
        }

        ReplaceFilterOptions(CollectionFilters, ["全部集合", .. collectionNames]);

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
        var filter = TagFilters.FirstOrDefault(candidate => string.Equals(candidate, preferredFilter, StringComparison.CurrentCultureIgnoreCase)) ?? "全部标签";
        if (SelectedTagFilter == filter)
        {
            OnPropertyChanged(nameof(SelectedTagFilter));
            return;
        }

        SelectedTagFilter = filter;
    }

    private void SelectCollectionFilter(string preferredFilter)
    {
        var filter = CollectionFilters.FirstOrDefault(candidate => string.Equals(candidate, preferredFilter, StringComparison.CurrentCultureIgnoreCase)) ?? "全部集合";
        if (SelectedCollectionFilter == filter)
        {
            OnPropertyChanged(nameof(SelectedCollectionFilter));
            return;
        }

        SelectedCollectionFilter = filter;
    }

    private static string BuildErrorMessage(Exception exception)
    {
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
