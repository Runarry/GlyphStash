using Avalonia.Controls;

namespace GlyphStash.Desktop;

internal sealed class AvaloniaTrayWindow : ITrayWindow
{
    private readonly Window _window;

    public AvaloniaTrayWindow(Window window)
    {
        _window = window;
    }

    public TrayWindowState State
    {
        get => FromAvalonia(_window.WindowState);
        set => _window.WindowState = ToAvalonia(value);
    }

    public bool ShowInTaskbar
    {
        get => _window.ShowInTaskbar;
        set => _window.ShowInTaskbar = value;
    }

    public void Show() => _window.Show();

    public void Hide() => _window.Hide();

    public void Activate() => _window.Activate();

    public static TrayWindowState FromAvalonia(WindowState state) =>
        state switch
        {
            WindowState.Minimized => TrayWindowState.Minimized,
            WindowState.Maximized => TrayWindowState.Maximized,
            WindowState.FullScreen => TrayWindowState.FullScreen,
            _ => TrayWindowState.Normal
        };

    public static WindowState ToAvalonia(TrayWindowState state) =>
        state switch
        {
            TrayWindowState.Minimized => WindowState.Minimized,
            TrayWindowState.Maximized => WindowState.Maximized,
            TrayWindowState.FullScreen => WindowState.FullScreen,
            _ => WindowState.Normal
        };
}
