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

internal readonly record struct TrayHideDecision(bool CanHide, string Reason)
{
    public static TrayHideDecision Allow() => new(true, "");

    public static TrayHideDecision Deny(string reason) => new(false, reason);
}

internal sealed class TrayWindowLifecycle
{
    private readonly ITrayWindow _window;
    private readonly Action _shutdown;
    private readonly Func<TrayHideDecision> _canHideToTray;
    private readonly Action<string> _hideBlocked;
    private readonly Action _hiddenToTray;
    private readonly Action _shownFromTray;
    private TrayWindowState _restoreState = TrayWindowState.Normal;
    private bool _isRestoring;
    private bool _isExiting;

    public TrayWindowLifecycle(
        ITrayWindow window,
        Action shutdown,
        Func<TrayHideDecision>? canHideToTray = null,
        Action<string>? hideBlocked = null,
        Action? hiddenToTray = null,
        Action? shownFromTray = null)
    {
        _window = window;
        _shutdown = shutdown;
        _canHideToTray = canHideToTray ?? TrayHideDecision.Allow;
        _hideBlocked = hideBlocked ?? (_ => { });
        _hiddenToTray = hiddenToTray ?? (() => { });
        _shownFromTray = shownFromTray ?? (() => { });
    }

    public bool IsHiddenToTray { get; private set; }

    public TrayHideDecision LastHideDecision { get; private set; } = TrayHideDecision.Allow();

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

    public bool HideToTray()
    {
        if (_isExiting || IsHiddenToTray)
        {
            return false;
        }

        var decision = _canHideToTray();
        LastHideDecision = decision;
        if (!decision.CanHide)
        {
            RestoreAfterBlockedHide(decision.Reason);
            return false;
        }

        var currentState = _window.State;
        if (currentState != TrayWindowState.Minimized)
        {
            _restoreState = currentState;
        }

        _window.ShowInTaskbar = false;
        _window.Hide();
        IsHiddenToTray = true;
        _hiddenToTray();
        return true;
    }

    public void ShowFromTray()
    {
        if (_isExiting)
        {
            return;
        }

        _shownFromTray();
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

    private void RestoreAfterBlockedHide(string reason)
    {
        _isRestoring = true;
        try
        {
            _window.ShowInTaskbar = true;
            _window.Show();
            _window.State = _restoreState == TrayWindowState.Minimized ? TrayWindowState.Normal : _restoreState;
            _window.Activate();
            IsHiddenToTray = false;
            _hideBlocked(reason);
        }
        finally
        {
            _isRestoring = false;
        }
    }
}
