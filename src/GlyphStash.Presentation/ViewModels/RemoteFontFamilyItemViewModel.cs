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

    public string FileName => _record.FileName;
}
