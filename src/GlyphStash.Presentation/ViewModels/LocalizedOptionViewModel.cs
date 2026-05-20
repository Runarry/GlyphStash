using CommunityToolkit.Mvvm.ComponentModel;
using GlyphStash.Localization;

namespace GlyphStash.Presentation.ViewModels;

public sealed partial class LocalizedOptionViewModel : ObservableObject
{
    private readonly string _label;
    private readonly string? _englishLabel;

    public LocalizedOptionViewModel(string value, string label, string? englishLabel = null)
    {
        Value = value;
        _label = label;
        _englishLabel = englishLabel;
    }

    public string Value { get; }

    public string DisplayName => AppText.CurrentCultureCode == AppText.EnglishCultureCode && _englishLabel is not null
        ? _englishLabel
        : AppText.TranslateLiteral(_label);

    public void RefreshLocalizedState() => OnPropertyChanged(nameof(DisplayName));

    public override string ToString() => DisplayName;
}
