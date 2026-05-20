using CommunityToolkit.Mvvm.ComponentModel;
using GlyphStash.Domain.Fonts;
using GlyphStash.Localization;

namespace GlyphStash.Presentation.ViewModels;

public sealed partial class FontFamilyItemViewModel : ObservableObject
{
    private FontFamilyRecord _record;

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private string _previewText = AppText.TranslateLiteral("GlyphStash 字体预览 Aa 123 你好");

    [ObservableProperty]
    private double _previewFontSize = 30;

    public FontFamilyItemViewModel(FontFamilyRecord record)
    {
        _record = record;
        IsFavorite = record.IsFavorite;
        Faces = record.Faces.Select(face => new FontFaceItemViewModel(face)).ToList();
    }

    public FontFamilyRecord ToRecord() => _record with { IsFavorite = IsFavorite };

    public string FamilyName => _record.FamilyName;

    public string StyleCountLabel => $"{_record.StyleCount} styles";

    public string SourceLabel => _record.SourceKind switch
    {
        FontSourceKind.System => L("系统字体"),
        FontSourceKind.UserInstalled => L("用户级安装"),
        FontSourceKind.GlyphStashManaged => L("GlyphStash 管理"),
        FontSourceKind.Temporary => L("临时字体"),
        _ => L("未知来源")
    };

    public string StateLabel => _record.ActivationState switch
    {
        FontActivationState.Installed => L("已安装"),
        FontActivationState.TemporarilyEnabled => L("已临时启用"),
        FontActivationState.NotEnabled => L("未启用"),
        _ => L("未知状态")
    };

    public string LicenseLabel => FormatLicenseLabel(_record.LicenseText);

    public string TagsLabel => _record.Tags.Count == 0 ? L("未设置标签") : string.Join("  ", _record.Tags);

    public IReadOnlyList<string> Tags => _record.Tags;

    public IReadOnlyList<string> Collections => _record.Collections;

    public IReadOnlyList<FontFaceItemViewModel> Faces { get; private set; } = [];

    public FontActivationState ActivationState => _record.ActivationState;

    public string VersionLabel => L("待解析");

    public string ManufacturerLabel => L("待解析");

    public string CoverageLabel => L("覆盖范围待解析");

    public string HashLabel => _record.Faces.FirstOrDefault()?.File.Sha256 ?? L("待计算");

    public string FilePath => string.IsNullOrWhiteSpace(_record.PrimaryFilePath) ? L("系统枚举字体，未解析文件路径") : _record.PrimaryFilePath;

    public string FormatSummary => string.IsNullOrWhiteSpace(_record.FormatSummary) ? "Installed" : _record.FormatSummary;

    public bool CanUninstall => _record.SourceKind is FontSourceKind.GlyphStashManaged or FontSourceKind.UserInstalled;

    public bool IsOkState => _record.ActivationState is FontActivationState.Installed or FontActivationState.TemporarilyEnabled;

    public bool IsWarningState => _record.ActivationState is FontActivationState.NotEnabled or FontActivationState.Unknown;

    public bool IsSystemSource => _record.SourceKind == FontSourceKind.System;

    public bool IsLicenseUnknown => _record.LicenseStatus != LicenseStatus.Known;

    public string FavoriteLabel => IsFavorite ? L("已收藏") : L("收藏");

    public string TemporaryActivationLabel => _record.ActivationState == FontActivationState.TemporarilyEnabled ? L("关闭临时启用") : L("临时启用");

    public bool CanTemporarilyActivate => _record.ActivationState != FontActivationState.Installed;

    public string TemporaryActivationDisabledReason => _record.ActivationState == FontActivationState.Installed ? L("已安装，无需临时启用") : "";

    public string InstallLabel => _record.ActivationState == FontActivationState.Installed ? L("已安装") : L("用户级安装");

    public void SetPreview(string text, double fontSize)
    {
        PreviewText = text;
        PreviewFontSize = fontSize;
    }

    public void SetActivationState(FontActivationState state)
    {
        _record = _record with { ActivationState = state };
        OnPropertyChanged(nameof(StateLabel));
        OnPropertyChanged(nameof(ActivationState));
        OnPropertyChanged(nameof(IsOkState));
        OnPropertyChanged(nameof(IsWarningState));
        OnPropertyChanged(nameof(TemporaryActivationLabel));
        OnPropertyChanged(nameof(CanTemporarilyActivate));
        OnPropertyChanged(nameof(TemporaryActivationDisabledReason));
        OnPropertyChanged(nameof(InstallLabel));
    }

    public void SetTagsAndCollections(IReadOnlyList<string> tags, IReadOnlyList<string> collections)
    {
        _record = _record with { Tags = tags, Collections = collections };
        OnPropertyChanged(nameof(TagsLabel));
        OnPropertyChanged(nameof(Tags));
        OnPropertyChanged(nameof(Collections));
    }

    public void RemoveTag(string tagName)
    {
        var tags = Tags
            .Where(tag => !string.Equals(tag, tagName, StringComparison.CurrentCultureIgnoreCase))
            .ToList();
        if (tags.Count == Tags.Count)
        {
            return;
        }

        SetTagsAndCollections(tags, Collections);
    }

    partial void OnIsFavoriteChanged(bool value)
    {
        OnPropertyChanged(nameof(FavoriteLabel));
    }

    private static string L(string text) => AppText.TranslateLiteral(text);

    public void RefreshLocalizedState()
    {
        OnPropertyChanged(nameof(SourceLabel));
        OnPropertyChanged(nameof(StateLabel));
        OnPropertyChanged(nameof(LicenseLabel));
        OnPropertyChanged(nameof(TagsLabel));
        OnPropertyChanged(nameof(VersionLabel));
        OnPropertyChanged(nameof(ManufacturerLabel));
        OnPropertyChanged(nameof(CoverageLabel));
        OnPropertyChanged(nameof(HashLabel));
        OnPropertyChanged(nameof(FilePath));
        OnPropertyChanged(nameof(FavoriteLabel));
        OnPropertyChanged(nameof(TemporaryActivationLabel));
        OnPropertyChanged(nameof(TemporaryActivationDisabledReason));
        OnPropertyChanged(nameof(InstallLabel));
    }

    private static string FormatLicenseLabel(string text)
    {
        if (string.IsNullOrWhiteSpace(text)
            || string.Equals(text, "未知授权", StringComparison.Ordinal)
            || string.Equals(text, "Unknown license", StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "Unknown licenses", StringComparison.OrdinalIgnoreCase))
        {
            return L("未知授权");
        }

        const string zhSourcePrefix = "请查看来源页面：";
        const string enSourcePrefix = "See source page: ";
        if (text.StartsWith(zhSourcePrefix, StringComparison.Ordinal))
        {
            return AppText.FormatLiteral("请查看来源页面：{0}", "See source page: {0}", text[zhSourcePrefix.Length..]);
        }

        if (text.StartsWith(enSourcePrefix, StringComparison.Ordinal))
        {
            return AppText.FormatLiteral("请查看来源页面：{0}", "See source page: {0}", text[enSourcePrefix.Length..]);
        }

        return AppText.TranslateLiteral(text);
    }

    public bool Matches(string searchText, string sourceFilter, string stateFilter, string tagFilter, string collectionFilter)
    {
        var sourceMatches = sourceFilter == "全部来源" || sourceFilter switch
        {
            "系统字体" => _record.SourceKind == FontSourceKind.System,
            "用户级安装" => _record.SourceKind == FontSourceKind.UserInstalled,
            "GlyphStash 管理" => _record.SourceKind == FontSourceKind.GlyphStashManaged,
            "临时字体" => _record.SourceKind == FontSourceKind.Temporary,
            _ => false
        };
        var stateMatches = stateFilter == "全部状态" || stateFilter switch
        {
            "已安装" => _record.ActivationState == FontActivationState.Installed,
            "已临时启用" => _record.ActivationState == FontActivationState.TemporarilyEnabled,
            "未启用" => _record.ActivationState == FontActivationState.NotEnabled,
            _ => false
        };
        var tagMatches = tagFilter == "全部标签" || Tags.Contains(tagFilter, StringComparer.CurrentCultureIgnoreCase);
        var collectionMatches = collectionFilter == "全部集合" || Collections.Contains(collectionFilter, StringComparer.CurrentCultureIgnoreCase);
        if (!sourceMatches || !stateMatches || !tagMatches || !collectionMatches)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        return FamilyName.Contains(searchText, StringComparison.CurrentCultureIgnoreCase)
            || Tags.Any(tag => tag.Contains(searchText, StringComparison.CurrentCultureIgnoreCase))
            || Faces.Any(face => face.FullName.Contains(searchText, StringComparison.CurrentCultureIgnoreCase));
    }
}
