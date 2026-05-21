using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;

namespace GlyphStash.Desktop;

[SupportedOSPlatform("windows")]
internal sealed class DesktopTrayController : IDisposable
{
    private static readonly Uri TrayIconUri = new("avares://GlyphStash.Desktop/Assets/glyphstash-tray.ico");

    private readonly Avalonia.Application _application;
    private readonly TrayWindowLifecycle _lifecycle;
    private readonly TrayIcon _trayIcon;
    private readonly TrayIcons _trayIcons;
    private readonly IDisposable _windowStateSubscription;
    private bool _disposed;

    public DesktopTrayController(Avalonia.Application application, MainWindow mainWindow, IClassicDesktopStyleApplicationLifetime desktop)
    {
        _application = application;
        _lifecycle = new TrayWindowLifecycle(new AvaloniaTrayWindow(mainWindow), () => desktop.Shutdown());
        _trayIcon = CreateTrayIcon();
        _trayIcons = [_trayIcon];
        TrayIcon.SetIcons(_application, _trayIcons);
        _trayIcon.Clicked += OnTrayIconClicked;
        _windowStateSubscription = mainWindow.GetObservable(Window.WindowStateProperty)
            .Subscribe(new WindowStateObserver(_lifecycle));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _windowStateSubscription.Dispose();
        _trayIcon.Clicked -= OnTrayIconClicked;
        _trayIcon.IsVisible = false;
        TrayIcon.SetIcons(_application, []);
        _trayIcons.Clear();
        _trayIcon.Dispose();
    }

    private TrayIcon CreateTrayIcon()
    {
        var menu = new NativeMenu();
        menu.Items.Add(new NativeMenuItem("打开 GlyphStash")
        {
            Command = new DelegateCommand(_lifecycle.ShowFromTray)
        });
        menu.Items.Add(new NativeMenuItem("隐藏到托盘")
        {
            Command = new DelegateCommand(_lifecycle.HideToTray)
        });
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(new NativeMenuItem("退出并关闭临时字体")
        {
            Command = new DelegateCommand(_lifecycle.Exit)
        });

        return new TrayIcon
        {
            Icon = LoadTrayIcon(),
            ToolTipText = "GlyphStash 正在运行",
            Menu = menu,
            IsVisible = true
        };
    }

    private static WindowIcon LoadTrayIcon()
    {
        using var stream = AssetLoader.Open(TrayIconUri);
        return new WindowIcon(stream);
    }

    private void OnTrayIconClicked(object? sender, EventArgs e) => _lifecycle.ShowFromTray();

    private sealed class WindowStateObserver : IObserver<WindowState>
    {
        private readonly TrayWindowLifecycle _lifecycle;

        public WindowStateObserver(TrayWindowLifecycle lifecycle)
        {
            _lifecycle = lifecycle;
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(WindowState value) =>
            _lifecycle.HandleWindowStateChanged(AvaloniaTrayWindow.FromAvalonia(value));
    }
}
