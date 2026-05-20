using GlyphStash.Localization;

namespace GlyphStash.Presentation.Tests;

public sealed class LocalizationResourceTests
{
    [Fact]
    public void ResourceKeys_MatchBetweenSupportedCultures()
    {
        var zh = AppText.GetResourceKeys("zh-CN");
        var en = AppText.GetResourceKeys("en-US");

        Assert.NotEmpty(zh);
        Assert.True(zh.SetEquals(en), "zh-CN and en-US resource keys must match.");
    }

    [Fact]
    public void Resources_FormatAndFallbackByCulture()
    {
        AppText.SetCulture("en-US");
        Assert.Equal("Configured", AppText.Get("Common.Configured"));
        Assert.Equal("UI language changed to: English", AppText.Format("Toast.LanguageChangedFormat", "English"));

        AppText.SetCulture("zh-CN");
        Assert.Equal("已配置", AppText.Get("Common.Configured"));
    }

    [Fact]
    public void LiteralResources_TranslateDynamicXamlText()
    {
        try
        {
            AppText.SetCulture("en-US");
            var onlineFontsKey = AppText.GetLiteralResourceKey("在线字体");
            var glyphMappingKey = AppText.GetLiteralResourceKey("Unicode 映射字形");
            var english = AppText.GetCurrentLiteralStrings();

            Assert.Equal("Online Fonts", english[onlineFontsKey]);
            Assert.Equal("Unicode-mapped glyphs", english[glyphMappingKey]);

            AppText.SetCulture("zh-CN");
            var chinese = AppText.GetCurrentLiteralStrings();

            Assert.Equal("在线字体", chinese[onlineFontsKey]);
            Assert.Equal("Unicode 映射字形", chinese[glyphMappingKey]);
        }
        finally
        {
            AppText.SetCulture("zh-CN");
        }
    }
}
