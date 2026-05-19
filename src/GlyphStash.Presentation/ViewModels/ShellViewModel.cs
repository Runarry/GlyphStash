using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlyphStash.Application.Fonts;
using GlyphStash.Domain.Fonts;

namespace GlyphStash.Presentation.ViewModels;

public sealed partial class ShellViewModel : ObservableObject
{
    private readonly FontLibraryService _fontLibraryService;
    private readonly List<FontFamilyItemViewModel> _allFonts = [];

    [ObservableProperty]
    private NavigationItemViewModel? _selectedNavigationItem;

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private string _selectedSourceFilter = "全部来源";

    [ObservableProperty]
    private string _selectedStateFilter = "全部状态";

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
    private string _scanStatus = "准备加载字体索引";

    [ObservableProperty]
    private string _statusMessage = "最近操作：等待字体索引";

    [ObservableProperty]
    private string _toastMessage = "";

    [ObservableProperty]
    private bool _isToastVisible;

    public ShellViewModel(FontLibraryService fontLibraryService)
    {
        _fontLibraryService = fontLibraryService;
    }

    public ObservableCollection<FontFamilyItemViewModel> Fonts { get; } = [];

    public ObservableCollection<NavigationItemViewModel> NavigationItems { get; } =
    [
        new("font-library", "字体库", "字体库", "查看、搜索和预览本地字体。", "M1", true),
        new("collections", "集合", "集合", "项目字体包、批量临时启用、关闭与导出清单。", "M2", false),
        new("online-fonts", "在线字体", "在线字体", "Google Fonts 搜索、授权信息与下载流程。", "M3", false),
        new("merge-tool", "合并工具", "合并工具", "指定 Unicode 范围合并字形并导出报告。", "M4", false),
        new("settings", "设置", "设置", "管理目录、缓存、API key、授权提示和诊断日志。", "M5", false),
        new("component-demo", "组件演示", "组件演示", "检查 M1 通用组件、状态和主题资源。", "M1", true)
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

    public bool HasFonts => Fonts.Count > 0;

    public bool IsEmpty => !IsBusy && Fonts.Count == 0;

    public bool HasSelectedFont => SelectedFont is not null;

    public bool HasNoSelectedFont => SelectedFont is null;

    public string FontCountLabel => _allFonts.Count == 0 ? "字体索引为空" : $"索引就绪：{_allFonts.Count:N0} 个字体族";

    public bool IsFontLibraryPage => SelectedNavigationItem?.IsFontLibrary == true;

    public bool IsComponentDemoPage => SelectedNavigationItem?.IsComponentDemo == true;

    public bool IsPlaceholderPage => SelectedNavigationItem is { IsImplemented: false };

    public string CurrentPageTitle => SelectedNavigationItem?.Title ?? "字体库";

    public string CurrentPageDescription => SelectedNavigationItem?.Description ?? "";

    public string CurrentPageMilestone => SelectedNavigationItem?.Milestone ?? "M1";

    public string EmptyStateMessage => _allFonts.Count == 0
        ? "字体索引为空。请重新扫描字体。"
        : $"当前筛选没有匹配字体。索引中共有 {_allFonts.Count:N0} 个字体族。";

    [RelayCommand]
    public async Task InitializeAsync()
    {
        SelectedNavigationItem ??= NavigationItems.First(item => item.Key == "font-library");
        IsBusy = true;
        ScanStatus = "正在读取 SQLite 字体索引...";
        try
        {
            var cached = await _fontLibraryService.LoadCachedFontsAsync(new FontSearchQuery(), CancellationToken.None);
            if (cached.Count == 0)
            {
                ScanStatus = "首次启动：正在扫描 Windows 字体目录...";
                cached = await _fontLibraryService.RescanAsync(CancellationToken.None);
            }

            ReplaceFonts(cached);
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
            ReplaceFonts(fonts);
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
    private void ToggleFavorite(FontFamilyItemViewModel? font)
    {
        if (font is null)
        {
            return;
        }

        font.IsFavorite = !font.IsFavorite;
        ShowToast(font.IsFavorite ? "已加入收藏" : "已取消收藏");
    }

    [RelayCommand]
    private void OpenImportDialog() => IsImportDialogOpen = true;

    [RelayCommand]
    private void CloseImportDialog() => IsImportDialogOpen = false;

    [RelayCommand]
    private void StartImport()
    {
        IsImportDialogOpen = false;
        ShowToast("M1 仅展示导入流程：真实安装/临时启用将在 M2 实现");
    }

    [RelayCommand]
    private void OpenTagsDialog() => IsTagsDialogOpen = true;

    [RelayCommand]
    private void CloseTagsDialog() => IsTagsDialogOpen = false;

    [RelayCommand]
    private void OpenUninstallDialog() => IsUninstallDialogOpen = true;

    [RelayCommand]
    private void CloseUninstallDialog() => IsUninstallDialogOpen = false;

    [RelayCommand]
    private void ConfirmUninstall()
    {
        IsUninstallDialogOpen = false;
        ShowToast("M1 不执行卸载：系统字体和非托管字体保持受保护");
    }

    partial void OnSearchTextChanged(string value) => ApplyFilters();

    partial void OnSelectedSourceFilterChanged(string value) => ApplyFilters();

    partial void OnSelectedStateFilterChanged(string value) => ApplyFilters();

    partial void OnSelectedFontChanged(FontFamilyItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelectedFont));
        OnPropertyChanged(nameof(HasNoSelectedFont));
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

    public string PreviewFontSizeLabel => $"{PreviewFontSize:0}px";

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
        OnPropertyChanged(nameof(FontCountLabel));
    }

    private void ApplyFilters()
    {
        var selectedName = SelectedFont?.FamilyName;
        Fonts.Clear();
        foreach (var font in _allFonts.Where(font => font.Matches(SearchText, SelectedSourceFilter, SelectedStateFilter)))
        {
            Fonts.Add(font);
        }

        SelectedFont = Fonts.FirstOrDefault(font => font.FamilyName == selectedName) ?? Fonts.FirstOrDefault();
        NotifyCollectionState();
    }

    private void NotifyCollectionState()
    {
        OnPropertyChanged(nameof(HasFonts));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasSelectedFont));
        OnPropertyChanged(nameof(HasNoSelectedFont));
        OnPropertyChanged(nameof(FontCountLabel));
        OnPropertyChanged(nameof(EmptyStateMessage));
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
