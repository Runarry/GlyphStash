using System.Globalization;
using GlyphStash.Localization;

namespace GlyphStash.Presentation.Services;

public interface IAppLocalizationService
{
    CultureInfo CurrentCulture { get; }

    IReadOnlyList<SupportedLanguage> SupportedLanguages { get; }

    event EventHandler? CultureChanged;

    string Get(string key);

    string Format(string key, params object?[] args);

    void SetCulture(string? cultureCode);

    void ApplyAvaloniaResources();
}
