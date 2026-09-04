using System.Diagnostics.CodeAnalysis;
using Avalonia.Threading;

namespace OpenConquer.Launcher.Diagnostics;

/// <summary>
/// Observes exception boundaries that escape normal launcher error handling.
/// </summary>
/// <remarks>
/// UI-dispatcher exceptions are intentionally not logged from the dispatcher callback itself.
/// Avalonia requires that callback to remain lightweight. The escaping exception is classified and
/// recorded later by the top-level process boundary.
/// </remarks>
internal sealed class LauncherHostExceptionObserver : IDisposable
{
    private readonly object _stateGate = new();
    private readonly LauncherDiagnostics _diagnostics;

    private Dispatcher? _uiDispatcher;
    private Exception? _pendingUiDispatcherException;
    private bool _started;
    private bool _disposed;

    public LauncherHostExceptionObserver(LauncherDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Begins observing process-wide background exception boundaries.
    /// </summary>
    public void Start()
    {
        lock (_stateGate)
        {
            ThrowIfDisposed();

            if (_started)
            {
                throw new InvalidOperationException("Launcher host exception observation has already started.");
            }

            AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;

            try
            {
                TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            }
            catch
            {
                AppDomain.CurrentDomain.UnhandledException -= OnAppDomainUnhandledException;

                throw;
            }

            _started = true;
        }
    }

    /// <summary>
    /// Attaches observation to the initialized Avalonia UI dispatcher.
    /// </summary>
    public void AttachUiDispatcher(Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        lock (_stateGate)
        {
            ThrowIfDisposed();

            if (!_started)
            {
                throw new InvalidOperationException("Launcher host exception observation must be started before attaching the UI dispatcher.");
            }

            if (_uiDispatcher is not null)
            {
                throw new InvalidOperationException("A launcher UI dispatcher has already been attached.");
            }

            dispatcher.UnhandledException += OnUiDispatcherUnhandledException;

            _uiDispatcher = dispatcher;
        }
    }

    /// <summary>
    /// Determines which host boundary produced an exception escaping the desktop lifetime.
    /// </summary>
    public LauncherExceptionDomain ClassifyTopLevelException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        lock (_stateGate)
        {
            ThrowIfDisposed();

            Exception? pendingUiDispatcherException = _pendingUiDispatcherException;

            _pendingUiDispatcherException = null;

            return ReferenceEquals(pendingUiDispatcherException, exception) ? LauncherExceptionDomain.UiDispatcher : LauncherExceptionDomain.TopLevel;
        }
    }

    public void Dispose()
    {
        Dispatcher? uiDispatcher;
        bool wasStarted;

        lock (_stateGate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            uiDispatcher = _uiDispatcher;
            _uiDispatcher = null;
            _pendingUiDispatcherException = null;

            wasStarted = _started;
            _started = false;
        }

        if (uiDispatcher is not null)
        {
            uiDispatcher.UnhandledException -= OnUiDispatcherUnhandledException;
        }

        if (wasStarted)
        {
            AppDomain.CurrentDomain.UnhandledException -= OnAppDomainUnhandledException;

            TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        }
    }

    internal void ObserveUiDispatcherException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        lock (_stateGate)
        {
            if (_disposed)
            {
                return;
            }

            _pendingUiDispatcherException = exception;
        }
    }

    private void OnUiDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs eventArgs)
    {
        ObserveUiDispatcherException(eventArgs.Exception);

        // Deliberately leave eventArgs.Handled unchanged. An unknown unhandled UI fault is not
        // considered safe to recover from globally.
    }

    private void OnAppDomainUnhandledException(object? sender, UnhandledExceptionEventArgs eventArgs)
    {
        if (eventArgs.ExceptionObject is not Exception exception)
        {
            return;
        }

        TryRecordException(LauncherExceptionDomain.AppDomain, eventArgs.IsTerminating, exception);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs eventArgs)
    {
        try
        {
            TryRecordException(LauncherExceptionDomain.UnobservedTask, isTerminating: false, eventArgs.Exception);
        }
        finally
        {
            eventArgs.SetObserved();
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A global exception observer must not throw a secondary diagnostics failure from an exception callback.")]
    private void TryRecordException(LauncherExceptionDomain domain, bool isTerminating, Exception exception)
    {
        try
        {
            _diagnostics.RecordException(domain, isTerminating, exception);
        }
        catch (Exception)
        {
            // Diagnostics are best-effort at an unhandled-exception boundary. Throwing here could
            // replace or obscure the process failure that this observer exists to preserve.
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
