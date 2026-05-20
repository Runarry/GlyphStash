using CommunityToolkit.Mvvm.ComponentModel;
using GlyphStash.Domain.Fonts;
using GlyphStash.Localization;

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

    public string Summary => AppText.CurrentCultureCode == AppText.EnglishCultureCode
        ? $"{_record.FontCount:N0} fonts · {_record.TemporarilyEnabledCount:N0} temporarily enabled · {UnknownLicenseLabel}"
        : $"{_record.FontCount:N0} 个字体 · {_record.TemporarilyEnabledCount:N0} 个已临时启用 · {UnknownLicenseLabel}";

    public string UnknownLicenseLabel => _record.UnknownLicenseCount == 0
        ? L("授权完整")
        : AppText.CurrentCultureCode == AppText.EnglishCultureCode
            ? $"{_record.UnknownLicenseCount:N0} unknown licenses"
            : $"{_record.UnknownLicenseCount:N0} 个未知授权";

    public string LastExportedLabel => _record.LastExportedAt is null ? L("未导出") : _record.LastExportedAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    public void Replace(FontCollectionRecord record)
    {
        _record = record;
        RefreshAllProperties();
    }

    public void RefreshLocalizedState() => RefreshAllProperties();

    private void RefreshAllProperties()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(FamilyNames));
        OnPropertyChanged(nameof(FontCount));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(UnknownLicenseLabel));
        OnPropertyChanged(nameof(LastExportedLabel));
    }

    private static string L(string text) => AppText.TranslateLiteral(text);
}
