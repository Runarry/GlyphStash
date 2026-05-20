using GlyphStash.Domain.Fonts;

namespace GlyphStash.Domain.Tests;

public sealed class UnicodeRangeParserTests
{
    [Fact]
    public void Parse_SupportsMultipleRangesAndSingleCodePoints()
    {
        var ranges = UnicodeRangeParser.Parse("U+4E00-U+4E02, U+3000; 0x41");

        Assert.Equal(
            ["U+0041", "U+3000", "U+4E00-U+4E02"],
            ranges.Select(range => range.Label));
        Assert.Equal(5, UnicodeRangeParser.CountCodePoints(ranges));
    }

    [Fact]
    public void Parse_MergesOverlappingAndAdjacentRanges()
    {
        var ranges = UnicodeRangeParser.Parse("U+0041-U+0043, U+0042, U+0044");

        Assert.Single(ranges);
        Assert.Equal("U+0041-U+0044", ranges[0].Label);
    }

    [Theory]
    [InlineData("")]
    [InlineData("U+110000")]
    [InlineData("U+D800")]
    [InlineData("U+D7FF-U+E000")]
    [InlineData("U+9FFF-U+4E00")]
    [InlineData("hello")]
    public void Parse_RejectsInvalidRanges(string value)
    {
        Assert.Throws<InvalidOperationException>(() => UnicodeRangeParser.Parse(value));
    }
}
