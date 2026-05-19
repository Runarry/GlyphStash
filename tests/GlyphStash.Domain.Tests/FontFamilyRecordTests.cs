using GlyphStash.Domain.Fonts;

namespace GlyphStash.Domain.Tests;

public sealed class FontFamilyRecordTests
{
    [Fact]
    public void StyleCount_ReflectsFaces()
    {
        var file = new FontFileRecord("C:/Fonts/Inter.ttf", "TTF", null, FontSourceKind.System, DateTimeOffset.UtcNow);
        var family = new FontFamilyRecord(
            "Inter",
            [
                new FontFaceRecord("Inter", "Regular", "Inter Regular", "Inter-Regular", 400, "Normal", "Normal", file),
                new FontFaceRecord("Inter", "Bold", "Inter Bold", "Inter-Bold", 700, "Normal", "Normal", file)
            ],
            FontSourceKind.System,
            FontActivationState.Installed,
            LicenseStatus.Unknown,
            "未知授权",
            [],
            [],
            false);

        Assert.Equal(2, family.StyleCount);
        Assert.Equal("C:/Fonts/Inter.ttf", family.PrimaryFilePath);
    }
}
