using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace GlyphStash.Desktop;

[SupportedOSPlatform("windows")]
internal sealed class DesktopTrayController : IDisposable
{
    private readonly Avalonia.Application _application;
    private readonly TrayWindowLifecycle _lifecycle;
    private readonly TrayIcon _trayIcon;
    private readonly TrayIcons _trayIcons;
    private readonly DesktopUiSessionManager _uiSessionManager;
    private bool _disposed;

    public DesktopTrayController(Avalonia.Application application, DesktopUiSessionManager uiSessionManager, IClassicDesktopStyleApplicationLifetime desktop)
    {
        _application = application;
        _uiSessionManager = uiSessionManager;
        _lifecycle = new TrayWindowLifecycle(
            uiSessionManager,
            () => desktop.Shutdown(),
            uiSessionManager.GetHideDecision,
            uiSessionManager.NotifyHideBlocked,
            uiSessionManager.ScheduleDeferredUnload,
            uiSessionManager.CancelDeferredUnload);
        _trayIcon = CreateTrayIcon();
        _trayIcons = [_trayIcon];
        TrayIcon.SetIcons(_application, _trayIcons);
        _trayIcon.Clicked += OnTrayIconClicked;
        _uiSessionManager.WindowStateChanged += OnWindowStateChanged;
        _uiSessionManager.HideToTrayRequested += OnHideToTrayRequested;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _uiSessionManager.WindowStateChanged -= OnWindowStateChanged;
        _uiSessionManager.HideToTrayRequested -= OnHideToTrayRequested;
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
            Command = new DelegateCommand(() => _lifecycle.HideToTray())
        });
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(new NativeMenuItem("退出并关闭临时字体")
        {
            Command = new DelegateCommand(_lifecycle.Exit)
        });

        return new TrayIcon
        {
            Icon = AppIcon.Load(),
            ToolTipText = "GlyphStash 正在运行",
            Menu = menu,
            IsVisible = true
        };
    }

    private void OnTrayIconClicked(object? sender, EventArgs e) => _lifecycle.ShowFromTray();

    private void OnWindowStateChanged(object? sender, TrayWindowState state) => _lifecycle.HandleWindowStateChanged(state);

    private void OnHideToTrayRequested(object? sender, EventArgs e) => _lifecycle.HideToTray();
}
