using GlyphStash.Domain.Fonts;

namespace GlyphStash.Presentation.ViewModels;

public sealed class GlyphItemViewModel
{
    private readonly GlyphRecord _record;

    public GlyphItemViewModel(GlyphRecord record)
    {
        _record = record;
    }

    public GlyphRecord ToRecord() => _record;

    public string Character => _record.Character;

    public string UnicodeLabel => _record.UnicodeLabel;

    public string GlyphName => _record.GlyphName;

    public string GlyphIdLabel => _record.GlyphId.ToString("N0");

    public string FaceName => _record.FaceName;
}
