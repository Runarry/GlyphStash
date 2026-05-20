using CommunityToolkit.Mvvm.ComponentModel;
using GlyphStash.Domain.Fonts;

namespace GlyphStash.Presentation.ViewModels;

public sealed partial class MergeRangeSegmentItemViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public MergeRangeSegmentItemViewModel(GlyphCoverageSegment segment)
    {
        Segment = segment;
    }

    public GlyphCoverageSegment Segment { get; }

    public UnicodeRange Range => Segment.Range;

    public string BlockName => Segment.BlockName;

    public string RangeLabel => Segment.RangeLabel;

    public string CountLabel => $"{Segment.CodePointCount:N0} 个码位";

    public string PresenceLabel => Segment.Presence switch
    {
        GlyphCoveragePresence.BaseOnly => "仅 A",
        GlyphCoveragePresence.SupplementalOnly => "仅 B",
        GlyphCoveragePresence.Both => "A+B",
        _ => "覆盖"
    };

    public bool IsBaseOnly => Segment.Presence == GlyphCoveragePresence.BaseOnly;

    public bool IsSupplementalOnly => Segment.Presence == GlyphCoveragePresence.SupplementalOnly;

    public bool IsBoth => Segment.Presence == GlyphCoveragePresence.Both;

    public string SampleLabel => Range.Start == Range.End
        ? CharacterFor(Range.Start)
        : $"{CharacterFor(Range.Start)} {CharacterFor(Range.End)}";

    private static string CharacterFor(int codePoint)
    {
        try
        {
            return char.ConvertFromUtf32(codePoint);
        }
        catch
        {
            return "";
        }
    }
}
