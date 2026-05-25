using Avalonia;
using System.Runtime.Versioning;
using Velopack;

namespace GlyphStash.Desktop;

[SupportedOSPlatform("windows")]
internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (WindowsUserDataUninstallCleanup.TryHandleUninstallPromptCommand(args))
        {
            return;
        }

        VelopackApp.Build()
            .OnAfterInstallFastCallback(_ => WindowsUserDataUninstallCleanup.TryInstallInteractiveUninstallPrompt())
            .OnAfterUpdateFastCallback(_ => WindowsUserDataUninstallCleanup.TryInstallInteractiveUninstallPrompt())
            .OnBeforeUninstallFastCallback(_ => WindowsUserDataUninstallCleanup.CleanupUserDataIfRequested())
            .Run();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}
