using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System.Runtime.Versioning;
using GlyphStash.Application.Abstractions.Fonts;
using GlyphStash.Application.Abstractions.Storage;
using GlyphStash.Application.Fonts;
using GlyphStash.Infrastructure.Fonts;
using GlyphStash.Infrastructure.Glyphs;
using GlyphStash.Infrastructure.Providers.GoogleFonts;
using GlyphStash.Infrastructure.Storage.Sqlite;
using GlyphStash.Platform.Windows.Fonts;
using GlyphStash.Presentation.Services;
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
            var mainWindow = new MainWindow
            {
                DataContext = shellViewModel
            };
            _serviceProvider.GetRequiredService<DesktopStorageDialogService>().TopLevel = mainWindow;
            desktop.MainWindow = mainWindow;

            desktop.MainWindow.Opened += async (_, _) =>
            {
                await _serviceProvider.GetRequiredService<FontActivationCoordinator>().MarkRestartedActivationsStaleAsync(CancellationToken.None);
                await shellViewModel.InitializeAsync();
            };
            desktop.Exit += (_, _) =>
            {
                try
                {
                    _serviceProvider.GetRequiredService<FontActivationCoordinator>()
                        .DeactivateAllOwnedActivationsAsync(CancellationToken.None)
                        .GetAwaiter()
                        .GetResult();
                }
                finally
                {
                    _serviceProvider.Dispose();
                }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    [SupportedOSPlatform("windows")]
    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        var dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GlyphStash");
        var dbPath = Path.Combine(dataDir, "glyphstash.db");

        services.AddSingleton<SqliteFontMetadataStore>(_ => new SqliteFontMetadataStore(dbPath));
        services.AddSingleton<IFontMetadataStore>(provider => provider.GetRequiredService<SqliteFontMetadataStore>());
        services.AddSingleton<IAppSettingsStore>(provider => provider.GetRequiredService<SqliteFontMetadataStore>());
        services.AddSingleton<IFontLibraryMutationStore>(provider => provider.GetRequiredService<SqliteFontMetadataStore>());
        services.AddSingleton<ITagStore>(provider => provider.GetRequiredService<SqliteFontMetadataStore>());
        services.AddSingleton<ICollectionStore>(provider => provider.GetRequiredService<SqliteFontMetadataStore>());
        services.AddSingleton<IActivationStore>(provider => provider.GetRequiredService<SqliteFontMetadataStore>());
        services.AddSingleton<IOperationLogStore>(provider => provider.GetRequiredService<SqliteFontMetadataStore>());
        services.AddSingleton<IDownloadRecordStore>(provider => provider.GetRequiredService<SqliteFontMetadataStore>());
        services.AddSingleton<IFontInventoryService, WindowsFontInventoryService>();
        services.AddSingleton<IFontMetadataReader, OpenTypeFontMetadataReader>();
        services.AddSingleton<IGlyphCatalogService, OpenTypeGlyphCatalogService>();
        services.AddSingleton<IManagedFontFileStore, ManagedFontFileStore>();
        services.AddSingleton<IWindowsFontApi, WindowsFontApi>();
        services.AddSingleton<IFontInstallService, WindowsFontInstallService>();
        services.AddSingleton<ITemporaryFontActivationService, WindowsTemporaryFontActivationService>();
        services.AddSingleton(new HttpClient());
        services.AddSingleton<IFontSourceProvider, GoogleFontsProvider>();
        services.AddSingleton<FontLibraryService>();
        services.AddSingleton<FontActivationCoordinator>();
        services.AddSingleton<LocalFontManagementService>();
        services.AddSingleton<OnlineFontService>();
        services.AddSingleton<DesktopStorageDialogService>();
        services.AddSingleton<IUserFileDialogService>(provider => provider.GetRequiredService<DesktopStorageDialogService>());
        services.AddSingleton<IUserClipboardService>(provider => provider.GetRequiredService<DesktopStorageDialogService>());
        services.AddSingleton<IFontPreviewRegistry, AvaloniaFontPreviewRegistry>();
        services.AddSingleton<ShellViewModel>();

        return services.BuildServiceProvider();
    }
}
