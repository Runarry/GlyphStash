using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System.Runtime.Versioning;
using GlyphStash.Application.Abstractions.Fonts;
using GlyphStash.Application.Abstractions.Storage;
using GlyphStash.Application.Fonts;
using GlyphStash.Infrastructure.Storage.Sqlite;
using GlyphStash.Platform.Windows.Fonts;
using GlyphStash.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GlyphStash.Desktop;

[SupportedOSPlatform("windows")]
public partial class App : Avalonia.Application
{
    private ServiceProvider? _serviceProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _serviceProvider = BuildServices();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var shellViewModel = _serviceProvider.GetRequiredService<ShellViewModel>();
            desktop.MainWindow = new MainWindow
            {
                DataContext = shellViewModel
            };

            desktop.MainWindow.Opened += async (_, _) => await shellViewModel.InitializeAsync();
            desktop.Exit += (_, _) => _serviceProvider.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    [SupportedOSPlatform("windows")]
    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        var dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GlyphStash");
        var dbPath = Path.Combine(dataDir, "glyphstash.db");

        services.AddSingleton<IFontInventoryService, WindowsFontInventoryService>();
        services.AddSingleton<IFontMetadataStore>(_ => new SqliteFontMetadataStore(dbPath));
        services.AddSingleton<FontLibraryService>();
        services.AddSingleton<ShellViewModel>();

        return services.BuildServiceProvider();
    }
}
