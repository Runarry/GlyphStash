using CommunityToolkit.Mvvm.ComponentModel;
using GlyphStash.Domain.Fonts;

namespace GlyphStash.Presentation.ViewModels;

public sealed partial class FontFaceItemViewModel : ObservableObject
{
    private readonly FontFaceRecord _record;

    [ObservableProperty]
    private bool _isSelected;

    public FontFaceItemViewModel(FontFaceRecord record)
    {
        _record = record;
    }

    public FontFaceRecord ToRecord() => _record;

    public string FamilyName => _record.FamilyName;

    public string StyleLabel => FontStyleVariantFormatter.FormatFaceStyle(_record.SubfamilyName, _record.Weight, _record.Slant);

    public string SubfamilyName => StyleLabel;

    public string FullName => _record.FullName;

    public string PostScriptName => _record.PostScriptName;

    public int Weight => _record.Weight;

    public string Slant => _record.Slant;

    public string FilePath => _record.File.Path;

    public string PreviewFontFamily => _record.FamilyName;

    public string FontWeightName => Weight switch
    {
        100 => "Thin",
        200 => "ExtraLight",
        300 => "Light",
        400 => "Normal",
        500 => "Medium",
        600 => "SemiBold",
        700 => "Bold",
        800 => "ExtraBold",
        900 => "Black",
        _ => "Normal"
    };

    public string FontStyleName => string.Equals(_record.Slant, "Italic", StringComparison.OrdinalIgnoreCase) ? "Italic" : "Normal";
}
