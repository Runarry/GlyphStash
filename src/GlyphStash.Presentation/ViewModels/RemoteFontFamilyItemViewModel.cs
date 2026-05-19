using CommunityToolkit.Mvvm.ComponentModel;
using GlyphStash.Domain.Fonts;

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

    public string SubsetsLabel => _record.Subsets.Count == 0 ? "未声明子集" : string.Join(", ", _record.Subsets.Take(5));

    public string LastModifiedLabel => _record.LastModified?.ToString("yyyy-MM-dd") ?? "未知更新日期";

    public string LicenseLabel => _record.LicenseText;

    public string SourceUrl => _record.SourceUrl;

    public string PreviewText => "The quick brown fox 跃过 123";

    public IReadOnlyList<RemoteFontStyle> SelectedStyles => Styles.Where(style => style.IsSelected).Select(style => style.ToRecord()).ToList();
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
        if (string.IsNullOrWhiteSpace(variant))
        {
            return "未知样式";
        }

        var normalized = variant.Trim();
        var isItalic = normalized.EndsWith("italic", StringComparison.OrdinalIgnoreCase);
        var weightText = isItalic ? normalized[..^6] : normalized;
        var weight = string.Equals(weightText, "regular", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(weightText)
            ? 400
            : int.TryParse(weightText, out var parsedWeight) ? parsedWeight : 0;
        var weightName = weight switch
        {
            100 => "Thin",
            200 => "Extra Light",
            300 => "Light",
            400 => "Regular",
            500 => "Medium",
            600 => "Semi Bold",
            700 => "Bold",
            800 => "Extra Bold",
            900 => "Black",
            _ => ""
        };

        if (string.IsNullOrWhiteSpace(weightName))
        {
            return normalized;
        }

        return isItalic ? $"{weightName} Italic {weight}" : $"{weightName} {weight}";
    }
}
