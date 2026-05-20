using GlyphStash.Localization;

namespace GlyphStash.Presentation.ViewModels;

public sealed class LanguageOptionViewModel
{
    public LanguageOptionViewModel(SupportedLanguage language)
    {
        CultureCode = language.CultureCode;
        DisplayName = language.DisplayName;
    }

    public string CultureCode { get; }

    public string DisplayName { get; }
}
