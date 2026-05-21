using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using GlyphStash.Application.Fonts;
using GlyphStash.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GlyphStash.Desktop;

[SupportedOSPlatform("windows")]
internal sealed class DesktopUiSessionManager : ITrayWindow, IDisposable
{
    internal static readonly TimeSpan DefaultUnloadDelay = TimeSpan.FromSeconds(30);

    private readonly IServiceProvider _rootProvider;
    private readonly IClassicDesktopStyleApplicationLifetime _desktop;
    private readonly TimeSpan _unloadDelay;
    private UiSession? _session;
    private CancellationTokenSource? _deferredUnloadCancellation;
    private bool _disposed;
    private bool _isClosingSession;
    private bool _restartRecoveryCompleted;
    private bool _showInTaskbar = true;
    private double? _lastWindowWidth;
    private double? _lastWindowHeight;

    public DesktopUiSessionManager(
        IServiceProvider rootProvider,
        IClassicDesktopStyleApplicationLifetime desktop,
        TimeSpan? unloadDelay = null)
    {
        _rootProvider = rootProvider;
        _desktop = desktop;
        _unloadDelay = unloadDelay ?? DefaultUnloadDelay;
    }

    public event EventHandler<TrayWindowState>? WindowStateChanged;

    public event EventHandler? HideToTrayRequested;

    public TrayWindowState State
    {
        get => _session is null ? TrayWindowState.Normal : AvaloniaTrayWindow.FromAvalonia(_session.Window.WindowState);
        set => EnsureSession().WindowState = AvaloniaTrayWindow.ToAvalonia(value);
    }

    public bool ShowInTaskbar
    {
        get => _session?.Window.ShowInTaskbar ?? _showInTaskbar;
        set
        {
            _showInTaskbar = value;
            if (_session is not null)
            {
                _session.Window.ShowInTaskbar = value;
            }
        }
    }

    public MainWindow EnsureSession()
    {
        ThrowIfDisposed();
        CancelDeferredUnload();

        if (_session is not null)
        {
            return _session.Window;
        }

        var scope = _rootProvider.CreateScope();
        var shellViewModel = scope.ServiceProvider.GetRequiredService<ShellViewModel>();
        var dialogService = scope.ServiceProvider.GetRequiredService<DesktopStorageDialogService>();
        var window = new MainWindow
        {
            DataContext = shellViewModel,
            ShowInTaskbar = _showInTaskbar
        };

        if (_lastWindowWidth is > 0)
        {
            window.Width = _lastWindowWidth.Value;
        }

        if (_lastWindowHeight is > 0)
        {
            window.Height = _lastWindowHeight.Value;
        }

        dialogService.TopLevel = window;
        var stateSubscription = window.GetObservable(Window.WindowStateProperty)
            .Subscribe(new WindowStateObserver(state => WindowStateChanged?.Invoke(this, state)));

        _session = new UiSession(scope, window, shellViewModel, dialogService, stateSubscription);
        window.Opened += OnWindowOpened;
        window.Closing += OnWindowClosing;
        _desktop.MainWindow = window;
        return window;
    }

    public void Show()
    {
        var window = EnsureSession();
        window.ShowInTaskbar = _showInTaskbar;
        window.Show();
    }

    public void Hide()
    {
        if (_session is null)
        {
            return;
        }

        _session.Window.Hide();
    }

    public void Activate()
    {
        _session?.Window.Activate();
    }

    public TrayHideDecision GetHideDecision()
    {
        if (_session?.ShellViewModel is not { } shellViewModel)
        {
            return TrayHideDecision.Allow();
        }

        return shellViewModel.CanHideToTray
            ? TrayHideDecision.Allow()
            : TrayHideDecision.Deny(shellViewModel.TrayHideBlockReason);
    }

    public void NotifyHideBlocked(string reason)
    {
        _session?.ShellViewModel.NotifyTrayHideBlocked(reason);
    }

    public void ScheduleDeferredUnload()
    {
        if (_session is null || _disposed)
        {
            return;
        }

        CancelDeferredUnload();
        var cancellation = new CancellationTokenSource();
        _deferredUnloadCancellation = cancellation;
        _ = UnloadAfterDelayAsync(cancellation.Token);
    }

    public void CancelDeferredUnload()
    {
        var cancellation = _deferredUnloadCancellation;
        if (cancellation is null)
        {
            return;
        }

        _deferredUnloadCancellation = null;
        cancellation.Cancel();
        cancellation.Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CancelDeferredUnload();
        DisposeCurrentSession(closeWindow: true);
    }

    private async Task UnloadAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_unloadDelay, cancellationToken).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    DisposeCurrentSession(closeWindow: true);
                }
            });
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async void OnWindowOpened(object? sender, EventArgs e)
    {
        if (_session is not { } session || !ReferenceEquals(sender, session.Window) || session.IsInitialized)
        {
            return;
        }

        session.IsInitialized = true;
        try
        {
            if (!_restartRecoveryCompleted)
            {
                _restartRecoveryCompleted = true;
                await _rootProvider.GetRequiredService<FontActivationCoordinator>()
                    .MarkRestartedActivationsStaleAsync(CancellationToken.None);
            }

            await session.ShellViewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            session.ShellViewModel.NotifyTrayHideBlocked(ex.Message);
        }
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_isClosingSession || _disposed)
        {
            return;
        }

        e.Cancel = true;
        HideToTrayRequested?.Invoke(this, EventArgs.Empty);
    }

    private void DisposeCurrentSession(bool closeWindow)
    {
        var session = _session;
        if (session is null)
        {
            return;
        }

        _session = null;
        CancelDeferredUnload();
        _lastWindowWidth = session.Window.Width;
        _lastWindowHeight = session.Window.Height;
        session.Window.Opened -= OnWindowOpened;
        session.Window.Closing -= OnWindowClosing;
        session.Detach();

        if (ReferenceEquals(_desktop.MainWindow, session.Window))
        {
            _desktop.MainWindow = null;
        }

        if (closeWindow)
        {
            _isClosingSession = true;
            try
            {
                session.Window.Close();
            }
            catch
            {
                // Window teardown is best effort; scope disposal below releases the heavy UI graph.
            }
            finally
            {
                _isClosingSession = false;
            }
        }

        session.Dispose();
        WindowsMemoryPressureReducer.TrimAfterUiUnload();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DesktopUiSessionManager));
        }
    }

    private sealed class WindowStateObserver : IObserver<WindowState>
    {
        private readonly Action<TrayWindowState> _onNext;

        public WindowStateObserver(Action<TrayWindowState> onNext)
        {
            _onNext = onNext;
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(WindowState value) => _onNext(AvaloniaTrayWindow.FromAvalonia(value));
    }

    private sealed class UiSession : IDisposable
    {
        private readonly IServiceScope _scope;
        private readonly IDisposable _stateSubscription;
        private readonly DesktopStorageDialogService _dialogService;
        private bool _detached;
        private bool _disposed;

        public UiSession(
            IServiceScope scope,
            MainWindow window,
            ShellViewModel shellViewModel,
            DesktopStorageDialogService dialogService,
            IDisposable stateSubscription)
        {
            _scope = scope;
            Window = window;
            ShellViewModel = shellViewModel;
            _dialogService = dialogService;
            _stateSubscription = stateSubscription;
        }

        public MainWindow Window { get; }

        public ShellViewModel ShellViewModel { get; }

        public bool IsInitialized { get; set; }

        public void Detach()
        {
            if (_detached)
            {
                return;
            }

            _detached = true;
            _stateSubscription.Dispose();
            _dialogService.TopLevel = null;
            Window.DataContext = null;
            Window.Content = null;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Detach();
            _scope.Dispose();
        }
    }
}
