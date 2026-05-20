using CommunityToolkit.Mvvm.ComponentModel;
using GlyphStash.Application.Fonts;
using GlyphStash.Localization;

namespace GlyphStash.Presentation.ViewModels;

public sealed class ImportPreviewItemViewModel : ObservableObject
{
    public ImportPreviewItemViewModel(FontImportPreviewItem item)
    {
        Item = item;
    }

    public FontImportPreviewItem Item { get; }

    public string FileName => Item.FileName;

    public string Format => string.IsNullOrWhiteSpace(Item.Format) ? L("未知") : Item.Format;

    public string FamilyName => string.IsNullOrWhiteSpace(Item.FamilyName) ? L("无法解析") : Item.FamilyName;

    public string Status => Item.ErrorMessage ?? AppText.TranslateLiteral(Item.Status);

    public string LicenseLabel =>
        string.IsNullOrWhiteSpace(Item.LicenseText)
            || string.Equals(Item.LicenseText, "未知授权", StringComparison.Ordinal)
            || string.Equals(Item.LicenseText, "Unknown license", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Item.LicenseText, "Unknown licenses", StringComparison.OrdinalIgnoreCase)
                ? L("未知授权")
                : AppText.TranslateLiteral(Item.LicenseText);

    public bool CanImport => Item.CanImport;

    public void RefreshLocalizedState()
    {
        OnPropertyChanged(nameof(Format));
        OnPropertyChanged(nameof(FamilyName));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(LicenseLabel));
    }

    private static string L(string text) => AppText.TranslateLiteral(text);
}
