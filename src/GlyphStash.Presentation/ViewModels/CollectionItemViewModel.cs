using CommunityToolkit.Mvvm.ComponentModel;
using GlyphStash.Domain.Fonts;

namespace GlyphStash.Presentation.ViewModels;

public sealed partial class CollectionItemViewModel : ObservableObject
{
    private FontCollectionRecord _record;

    public CollectionItemViewModel(FontCollectionRecord record)
    {
        _record = record;
    }

    public string Name => _record.Name;

    public IReadOnlyList<string> FamilyNames => _record.FamilyNames;

    public int FontCount => _record.FontCount;

    public string Summary => $"{_record.FontCount:N0} 个字体 · {_record.TemporarilyEnabledCount:N0} 个已临时启用 · {UnknownLicenseLabel}";

    public string UnknownLicenseLabel => _record.UnknownLicenseCount == 0 ? "授权完整" : $"{_record.UnknownLicenseCount:N0} 个未知授权";

    public string LastExportedLabel => _record.LastExportedAt is null ? "未导出" : _record.LastExportedAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    public void Replace(FontCollectionRecord record)
    {
        _record = record;
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(FamilyNames));
        OnPropertyChanged(nameof(FontCount));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(UnknownLicenseLabel));
        OnPropertyChanged(nameof(LastExportedLabel));
    }
}
