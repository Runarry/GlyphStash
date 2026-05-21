using GlyphStash.Desktop;

namespace GlyphStash.Desktop.Tests;

public sealed class TrayWindowLifecycleTests
{
    [Fact]
    public void MinimizedWindow_HidesToTrayWithoutShutdown()
    {
        var window = new FakeTrayWindow { State = TrayWindowState.Normal };
        var shutdownCalls = 0;
        var lifecycle = new TrayWindowLifecycle(window, () => shutdownCalls++);

        lifecycle.HandleWindowStateChanged(TrayWindowState.Minimized);

        Assert.True(lifecycle.IsHiddenToTray);
        Assert.False(window.ShowInTaskbar);
        Assert.Equal(1, window.HideCalls);
        Assert.Equal(0, window.ShowCalls);
        Assert.Equal(0, shutdownCalls);
    }

    [Fact]
    public void ShowFromTray_RestoresPreviousNonMinimizedState()
    {
        var window = new FakeTrayWindow { State = TrayWindowState.Normal };
        var lifecycle = new TrayWindowLifecycle(window, () => { });

        lifecycle.HandleWindowStateChanged(TrayWindowState.Maximized);
        window.State = TrayWindowState.Minimized;
        lifecycle.HandleWindowStateChanged(TrayWindowState.Minimized);
        lifecycle.ShowFromTray();

        Assert.False(lifecycle.IsHiddenToTray);
        Assert.True(window.ShowInTaskbar);
        Assert.Equal(TrayWindowState.Maximized, window.State);
        Assert.Equal(1, window.ShowCalls);
        Assert.Equal(1, window.ActivateCalls);
    }

    [Fact]
    public void ExplicitHideToTray_KeepsTemporaryActivationOwnedByTheProcess()
    {
        var window = new FakeTrayWindow { State = TrayWindowState.Normal };
        var shutdownCalls = 0;
        var lifecycle = new TrayWindowLifecycle(window, () => shutdownCalls++);

        lifecycle.HideToTray();

        Assert.True(lifecycle.IsHiddenToTray);
        Assert.Equal(0, shutdownCalls);
    }

    [Fact]
    public void Exit_ShutsDownApplicationOnce()
    {
        var window = new FakeTrayWindow { State = TrayWindowState.Normal };
        var shutdownCalls = 0;
        var lifecycle = new TrayWindowLifecycle(window, () => shutdownCalls++);

        lifecycle.Exit();
        lifecycle.Exit();
        lifecycle.HandleWindowStateChanged(TrayWindowState.Minimized);

        Assert.Equal(1, shutdownCalls);
        Assert.False(lifecycle.IsHiddenToTray);
        Assert.Equal(0, window.HideCalls);
    }

    private sealed class FakeTrayWindow : ITrayWindow
    {
        public TrayWindowState State { get; set; }

        public bool ShowInTaskbar { get; set; } = true;

        public int ShowCalls { get; private set; }

        public int HideCalls { get; private set; }

        public int ActivateCalls { get; private set; }

        public void Show() => ShowCalls++;

        public void Hide() => HideCalls++;

        public void Activate() => ActivateCalls++;
    }
}
