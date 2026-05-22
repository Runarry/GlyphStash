using CommunityToolkit.Mvvm.ComponentModel;
using GlyphStash.Domain.Fonts;
using GlyphStash.Localization;
using static GlyphStash.Localization.AppTextExtensions;

namespace GlyphStash.Presentation.ViewModels;

public sealed partial class RemoteFontFamilyItemViewModel : ObservableObject
{
    private readonly RemoteFontFamily _record;

    public RemoteFontFamilyItemViewModel(RemoteFontFamily record)
    {
        _record = record;
        Styles = record.Styles.Select(style => new RemoteFontStyleOptionViewModel(style)).ToList();
    }

    public RemoteFontFamily ToRecord() => _record;

    public IReadOnlyList<RemoteFontStyleOptionViewModel> Styles { get; }

    public string FamilyName => _record.FamilyName;

    public string CategoryLabel => $"{_record.Category} · {_record.Styles.Count} styles";

    public string SubsetsLabel => _record.Subsets.Count == 0 ? L("未声明子集") : string.Join(", ", _record.Subsets.Take(5));

    public string LastModifiedLabel => _record.LastModified?.ToString("yyyy-MM-dd") ?? L("未知更新日期");

    public string LicenseLabel => FormatLicenseLabel(_record.LicenseText);

    public string SourceUrl => _record.SourceUrl;

    public string PreviewText => AppText.CurrentCultureCode == AppText.EnglishCultureCode ? "The quick brown fox jumps 123" : "The quick brown fox 跃过 123";

    public IReadOnlyList<RemoteFontStyle> SelectedStyles => Styles.Where(style => style.IsSelected).Select(style => style.ToRecord()).ToList();

    public void RefreshLocalizedState()
    {
        OnPropertyChanged(nameof(SubsetsLabel));
        OnPropertyChanged(nameof(LastModifiedLabel));
        OnPropertyChanged(nameof(LicenseLabel));
        OnPropertyChanged(nameof(PreviewText));
    }

    private static string FormatLicenseLabel(string text)
    {
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
}

public sealed partial class RemoteFontStyleOptionViewModel : ObservableObject
{
    private readonly RemoteFontStyle _record;

    [ObservableProperty]
    private bool _isSelected;

    public RemoteFontStyleOptionViewModel(RemoteFontStyle record)
    {
        _record = record;
        IsSelected = record.IsSelected;
    }

    public RemoteFontStyle ToRecord() => _record with { IsSelected = IsSelected };

    public string Variant => _record.Variant;

    public string DisplayLabel => FormatVariantLabel(_record.Variant);

    public string FileName => _record.FileName;

    public static string FormatVariantLabel(string variant)
    {
        var label = FontStyleVariantFormatter.FormatGoogleFontsVariant(variant);
        return string.IsNullOrWhiteSpace(label) ? AppText.TranslateLiteral("未知样式") : label;
    }
}
