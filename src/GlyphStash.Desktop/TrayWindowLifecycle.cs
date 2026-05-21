namespace GlyphStash.Desktop;

internal enum TrayWindowState
{
    Normal,
    Minimized,
    Maximized,
    FullScreen
}

internal interface ITrayWindow
{
    TrayWindowState State { get; set; }

    bool ShowInTaskbar { get; set; }

    void Show();

    void Hide();

    void Activate();
}

internal sealed class TrayWindowLifecycle
{
    private readonly ITrayWindow _window;
    private readonly Action _shutdown;
    private TrayWindowState _restoreState = TrayWindowState.Normal;
    private bool _isRestoring;
    private bool _isExiting;

    public TrayWindowLifecycle(ITrayWindow window, Action shutdown)
    {
        _window = window;
        _shutdown = shutdown;
    }

    public bool IsHiddenToTray { get; private set; }

    public void HandleWindowStateChanged(TrayWindowState state)
    {
        if (_isExiting || _isRestoring)
        {
            return;
        }

        if (state == TrayWindowState.Minimized)
        {
            HideToTray();
            return;
        }

        _restoreState = state;
    }

    public void HideToTray()
    {
        if (_isExiting || IsHiddenToTray)
        {
            return;
        }

        var currentState = _window.State;
        if (currentState != TrayWindowState.Minimized)
        {
            _restoreState = currentState;
        }

        _window.ShowInTaskbar = false;
        _window.Hide();
        IsHiddenToTray = true;
    }

    public void ShowFromTray()
    {
        if (_isExiting)
        {
            return;
        }

        _isRestoring = true;
        try
        {
            _window.ShowInTaskbar = true;
            _window.Show();
            _window.State = _restoreState == TrayWindowState.Minimized ? TrayWindowState.Normal : _restoreState;
            _window.Activate();
            IsHiddenToTray = false;
        }
        finally
        {
            _isRestoring = false;
        }
    }

    public void Exit()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        _shutdown();
    }
}
