using GlyphStash.Domain.Fonts;

namespace GlyphStash.Presentation.ViewModels;

public sealed class MergeRangeBlockItemViewModel
{
    public MergeRangeBlockItemViewModel(
        string name,
        int start,
        int end,
        int baseCodePointCount,
        int supplementalCodePointCount,
        int sharedCodePointCount,
        bool isOther = false)
    {
        Name = name;
        Start = start;
        End = end;
        BaseCodePointCount = baseCodePointCount;
        SupplementalCodePointCount = supplementalCodePointCount;
        SharedCodePointCount = sharedCodePointCount;
        IsOther = isOther;
    }

    public string Name { get; }

    public int Start { get; }

    public int End { get; }

    public int BaseCodePointCount { get; }

    public int SupplementalCodePointCount { get; }

    public int SharedCodePointCount { get; }

    public bool IsOther { get; }

    public bool IsAll => string.Equals(Name, UnicodeCoverageBlocks.AllBlocks, StringComparison.Ordinal);

    public string RangeLabel => IsAll || IsOther ? "" : new UnicodeRange(Start, End).Label;

    public string Summary =>
        $"A {BaseCodePointCount:N0} · B {SupplementalCodePointCount:N0} · A+B {SharedCodePointCount:N0}";

    public string DisplayLabel => string.IsNullOrWhiteSpace(RangeLabel) ? Name : $"{Name} · {RangeLabel}";
}
