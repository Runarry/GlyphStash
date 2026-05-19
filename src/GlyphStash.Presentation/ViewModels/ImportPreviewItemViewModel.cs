using GlyphStash.Application.Fonts;

namespace GlyphStash.Presentation.ViewModels;

public sealed class ImportPreviewItemViewModel
{
    public ImportPreviewItemViewModel(FontImportPreviewItem item)
    {
        Item = item;
    }

    public FontImportPreviewItem Item { get; }

    public string FileName => Item.FileName;

    public string Format => string.IsNullOrWhiteSpace(Item.Format) ? "未知" : Item.Format;

    public string FamilyName => string.IsNullOrWhiteSpace(Item.FamilyName) ? "无法解析" : Item.FamilyName;

    public string Status => Item.ErrorMessage ?? Item.Status;

    public string LicenseLabel => string.IsNullOrWhiteSpace(Item.LicenseText) ? "未知授权" : Item.LicenseText;

    public bool CanImport => Item.CanImport;
}
