using GlyphStash.Localization;
using static GlyphStash.Localization.AppTextExtensions;

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

    [Fact]
    public void LiteralHelpers_TranslateAndFormatByCulture()
    {
        try
        {
            AppText.SetCulture("en-US");
            Assert.Equal("Online Fonts", L("在线字体"));
            Assert.Equal("Downloaded 3 styles.", F("已下载 {0} 个样式。", "Downloaded {0} styles.", 3));

            AppText.SetCulture("zh-CN");
            Assert.Equal("在线字体", L("在线字体"));
            Assert.Equal("已下载 3 个样式。", F("已下载 {0} 个样式。", "Downloaded {0} styles.", 3));
        }
        finally
        {
            AppText.SetCulture("zh-CN");
        }
    }

    [Fact]
    public void ToUserMessage_PrefersInvalidOperationMessage()
    {
        var exception = new Exception("outer", new InvalidOperationException("actionable", new Exception("inner")));

        Assert.Equal("actionable", exception.ToUserMessage());
    }

    [Fact]
    public void ToUserMessage_FallsBackToDeepestMessageOrTypeName()
    {
        var nested = new Exception("outer", new Exception("deepest"));
        var empty = new Exception("");

        Assert.Equal("deepest", nested.ToUserMessage());
        Assert.Equal(nameof(Exception), empty.ToUserMessage());
    }
}
