using System.Globalization;
using Avalonia;
using GlyphStash.Localization;

namespace GlyphStash.Presentation.Services;

public sealed class AppLocalizationService : IAppLocalizationService
{
    public AppLocalizationService()
    {
        AppText.CultureChanged += OnAppTextCultureChanged;
    }

    public CultureInfo CurrentCulture => AppText.CurrentCulture;

    public IReadOnlyList<SupportedLanguage> SupportedLanguages => AppText.SupportedLanguages;

    public event EventHandler? CultureChanged;

    public string Get(string key) => AppText.Get(key);

    public string Format(string key, params object?[] args) => AppText.Format(key, args);

    public void SetCulture(string? cultureCode) => AppText.SetCulture(cultureCode);

    public void ApplyAvaloniaResources()
    {
        if (Avalonia.Application.Current is null)
        {
            return;
        }

        foreach (var item in AppText.GetCurrentStrings())
        {
            Avalonia.Application.Current.Resources[$"L.{item.Key}"] = item.Value;
        }

        foreach (var item in AppText.GetCurrentLiteralStrings())
        {
            Avalonia.Application.Current.Resources[item.Key] = item.Value;
        }
    }

    private void OnAppTextCultureChanged(object? sender, EventArgs e)
    {
        ApplyAvaloniaResources();
        CultureChanged?.Invoke(this, EventArgs.Empty);
    }
}
