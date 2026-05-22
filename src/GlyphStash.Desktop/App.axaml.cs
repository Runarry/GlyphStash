using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System.Runtime.Versioning;
using GlyphStash.Application.Abstractions.Fonts;
using GlyphStash.Application.Abstractions.Storage;
using GlyphStash.Application.Fonts;
using GlyphStash.Infrastructure.Fonts;
using GlyphStash.Infrastructure.FontTools;
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
    private DesktopUiSessionManager? _uiSessionManager;
    private DesktopTrayController? _trayController;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _serviceProvider = BuildServices();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _serviceProvider.GetRequiredService<IAppLocalizationService>().ApplyAvaloniaResources();
            _uiSessionManager = new DesktopUiSessionManager(_serviceProvider, desktop);
            _trayController = new DesktopTrayController(this, _uiSessionManager, desktop);
            _uiSessionManager.EnsureSession();
            desktop.Exit += (_, _) =>
            {
                try
                {
                    try
                    {
                        _trayController?.Dispose();
                    }
                    catch
                    {
                        // Tray teardown must not block temporary font cleanup.
                    }

                    try
                    {
                        _uiSessionManager?.Dispose();
                    }
                    catch
                    {
                        // UI teardown is best effort; temporary font cleanup still has priority.
                    }

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
    internal static ServiceProvider BuildServices()
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
        services.AddSingleton<IWindowsFontApi, WindowsFontApi>();
        services.AddSingleton<ITemporaryFontActivationService, WindowsTemporaryFontActivationService>();
        services.AddSingleton<FontActivationCoordinator>();
        services.AddSingleton<IAppLocalizationService, AppLocalizationService>();

        services.AddScoped<IFontInventoryService, WindowsFontInventoryService>();
        services.AddScoped<IFontMetadataReader, OpenTypeFontMetadataReader>();
        services.AddScoped<IGlyphCatalogService, OpenTypeGlyphCatalogService>();
        services.AddScoped<IFontMergeWorker, FontToolsWorkerClient>();
        services.AddScoped<IManagedFontFileStore, ManagedFontFileStore>();
        services.AddScoped<IFontInstallService, WindowsFontInstallService>();
        services.AddScoped(_ => new HttpClient());
        services.AddScoped<IFontSourceProvider, GoogleFontsProvider>();
        services.AddScoped<IFontLibraryService, FontLibraryService>();
        services.AddScoped<IOperationLogger, OperationLogger>();
        services.AddScoped<ILocalFontSettingsService, LocalFontSettingsService>();
        services.AddScoped<ILocalFontImportService, LocalFontImportService>();
        services.AddScoped<ILocalFontOrganizationService, LocalFontOrganizationService>();
        services.AddScoped<ILocalFontActivationService, LocalFontActivationService>();
        services.AddScoped<ILocalFontOperationLogService, LocalFontOperationLogService>();
        services.AddScoped<ILocalFontManagementService, LocalFontManagementService>();
        services.AddScoped<IOnlineFontService, OnlineFontService>();
        services.AddScoped<IFontMergeService, FontMergeService>();
        services.AddScoped<DesktopStorageDialogService>();
        services.AddScoped<IUserFileDialogService>(provider => provider.GetRequiredService<DesktopStorageDialogService>());
        services.AddScoped<IUserClipboardService>(provider => provider.GetRequiredService<DesktopStorageDialogService>());
        services.AddScoped<IFontPreviewRegistry, AvaloniaFontPreviewRegistry>();
        services.AddScoped<ShellViewModel>();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }
}
