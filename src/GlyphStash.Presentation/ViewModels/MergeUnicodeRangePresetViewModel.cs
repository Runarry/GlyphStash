using CommunityToolkit.Mvvm.ComponentModel;
using GlyphStash.Domain.Fonts;
using GlyphStash.Localization;

namespace GlyphStash.Presentation.ViewModels;

public sealed partial class MergeUnicodeRangePresetViewModel : ObservableObject
{
    private readonly MergeUnicodeRangePresetDefinition _definition;

    public MergeUnicodeRangePresetViewModel(MergeUnicodeRangePresetDefinition definition)
    {
        _definition = definition;
    }

    public string Key => _definition.Key;

    public string DisplayName => AppText.CurrentCultureCode == AppText.EnglishCultureCode
        ? _definition.EnglishLabel
        : _definition.Label;

    public string Description => AppText.CurrentCultureCode == AppText.EnglishCultureCode
        ? _definition.EnglishDescription
        : _definition.Description;

    public string RangeText => _definition.RangeText;

    public string RangeCountLabel => AppText.CurrentCultureCode == AppText.EnglishCultureCode
        ? $"{_definition.Ranges.Count:N0} ranges"
        : $"{_definition.Ranges.Count:N0} 段范围";

    public void RefreshLocalizedState()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(RangeCountLabel));
    }
}

public sealed record MergeUnicodeRangePresetDefinition(
    string Key,
    string Label,
    string EnglishLabel,
    string Description,
    string EnglishDescription,
    IReadOnlyList<UnicodeRange> Ranges)
{
    public string RangeText => string.Join(", ", Ranges.Select(range => range.Label));
}

public static class MergeUnicodeRangePresetCatalog
{
    public static IReadOnlyList<MergeUnicodeRangePresetDefinition> Presets { get; } =
    [
        new(
            "chinese",
            "中文",
            "Chinese",
            "常用汉字、扩展 A、中文标点与全半角符号。",
            "Common CJK ideographs, Extension A, CJK punctuation, and fullwidth forms.",
            [
                new UnicodeRange(0x3000, 0x303F),
                new UnicodeRange(0x3400, 0x4DBF),
                new UnicodeRange(0x4E00, 0x9FFF),
                new UnicodeRange(0xF900, 0xFAFF),
                new UnicodeRange(0xFE30, 0xFE4F),
                new UnicodeRange(0xFF00, 0xFFEF)
            ]),
        new(
            "latin",
            "拉丁语系",
            "Latin scripts",
            "英文、西欧文字与常用扩展拉丁字符。",
            "English, Western European text, and common extended Latin characters.",
            [
                new UnicodeRange(0x0000, 0x024F),
                new UnicodeRange(0x1E00, 0x1EFF)
            ]),
        new(
            "japanese",
            "日文",
            "Japanese",
            "平假名、片假名、日文标点、常用汉字与全半角符号。",
            "Hiragana, katakana, Japanese punctuation, common kanji, and half/fullwidth forms.",
            [
                new UnicodeRange(0x3000, 0x303F),
                new UnicodeRange(0x3040, 0x309F),
                new UnicodeRange(0x30A0, 0x30FF),
                new UnicodeRange(0x31F0, 0x31FF),
                new UnicodeRange(0x4E00, 0x9FFF),
                new UnicodeRange(0xF900, 0xFAFF),
                new UnicodeRange(0xFF00, 0xFFEF)
            ]),
        new(
            "emoji",
            "Emoji",
            "Emoji",
            "表情、符号、旗帜、补充图符与变体选择符。",
            "Emoticons, symbols, flags, supplemental pictographs, and variation selectors.",
            [
                new UnicodeRange(0x2600, 0x26FF),
                new UnicodeRange(0x2700, 0x27BF),
                new UnicodeRange(0xFE00, 0xFE0F),
                new UnicodeRange(0x1F1E6, 0x1F1FF),
                new UnicodeRange(0x1F300, 0x1F5FF),
                new UnicodeRange(0x1F600, 0x1F64F),
                new UnicodeRange(0x1F680, 0x1F6FF),
                new UnicodeRange(0x1F780, 0x1F7FF),
                new UnicodeRange(0x1F900, 0x1F9FF),
                new UnicodeRange(0x1FA70, 0x1FAFF)
            ])
    ];
}
