using System.IO.Pipes;
using System.Runtime.ExceptionServices;

namespace OpenConquer.Launcher.Instances;

internal sealed class LauncherActivationServer : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly NamedPipeServerStream _pipe;
    private readonly CancellationTokenSource _lifetime;
    private readonly TimeSpan _requestBudget;
    private Task? _run;
    private Task? _stop;

    public LauncherActivationServer(string pipeName) : this(pipeName, TimeSpan.FromSeconds(1))
    {
    }

    internal LauncherActivationServer(string pipeName, TimeSpan requestBudget)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        if (requestBudget <= TimeSpan.Zero || requestBudget > TimeSpan.FromSeconds(5))
        {
            throw new ArgumentOutOfRangeException(nameof(requestBudget));
        }

        _requestBudget = requestBudget;
        PipeOptions options = PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly;
        if (OperatingSystem.IsWindows())
        {
            options |= PipeOptions.FirstPipeInstance;
        }
        else
        {
            UnixActivationNamespace.ValidateEndpoint(pipeName);
        }

        // On Unix, the runtime removes stale socket entries. The process lease must already be
        // held before constructing this server, and remain held until it has been disposed.
        _pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, options);
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(pipeName, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            _lifetime = new CancellationTokenSource();
        }
        catch
        {
            _pipe.Dispose();
            throw;
        }
    }

    public Task RunAsync(Func<CancellationToken, Task<bool>> activate)
    {
        ArgumentNullException.ThrowIfNull(activate);
        lock (_gate)
        {
            if (_run is not null || _stop is not null)
            {
                throw new InvalidOperationException("The activation listener can only start once, before shutdown.");
            }

            _run = Task.Run(() => ListenAsync(activate, _lifetime.Token), CancellationToken.None);
            return _run;
        }
    }

    public Task StopAsync()
    {
        lock (_gate)
        {
            return _stop ??= DrainAsync(_lifetime.CancelAsync(), _run ?? Task.CompletedTask);
        }
    }

    public ValueTask DisposeAsync() => new(StopAsync());

    private async Task DrainAsync(Task cancellation, Task listener)
    {
        Task drain = Task.WhenAll(cancellation, listener);
        try
        {
            await drain.ConfigureAwait(false);
        }
        catch
        {
            if (drain.Exception is not null)
            {
                ExceptionDispatchInfo.Capture(drain.Exception).Throw();
            }

            throw;
        }
        finally
        {
            _pipe.Dispose();
            _lifetime.Dispose();
        }
    }

    private async Task ListenAsync(Func<CancellationToken, Task<bool>> activate, CancellationToken lifetime)
    {
        byte[] request = new byte[LauncherActivationProtocol.RequestLength];
        byte[] reply = new byte[1];
        try
        {
            while (true)
            {
                lifetime.ThrowIfCancellationRequested();
                try
                {
                    await _pipe.WaitForConnectionAsync(lifetime).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
                {
                    // Unix named pipes use an internal linked token for accept cancellation.
                    return;
                }
                catch (UnauthorizedAccessException)
                {
                    // CurrentUserOnly rejected this peer before it could supply a command.
                    continue;
                }

                using CancellationTokenSource requestLifetime = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
                requestLifetime.CancelAfter(_requestBudget);
                CancellationToken token = requestLifetime.Token;
                try
                {
                    bool valid;
                    try
                    {
                        await _pipe.ReadExactlyAsync(request, token).ConfigureAwait(false);
                        valid = LauncherActivationProtocol.IsActivationRequest(request);
                    }
                    catch (IOException)
                    {
                        continue;
                    }

                    if (!valid)
                    {
                        continue;
                    }

                    // Keep application failures outside the transport-error catch below.
                    reply[0] = await activate(token).ConfigureAwait(false)
                        ? LauncherActivationProtocol.Accepted : LauncherActivationProtocol.Unavailable;
                    try
                    {
                        await _pipe.WriteAsync(reply, token).ConfigureAwait(false);
                    }
                    catch (IOException)
                    {
                        // The peer may exit after requesting activation.
                    }
                }
                catch (OperationCanceledException exception) when (token.IsCancellationRequested && exception.CancellationToken == token)
                {
                    // A slow peer gets one fixed deadline, never an unbounded read or UI wait.
                }
                finally
                {
                    _pipe.Disconnect();
                }
            }
        }
        catch (OperationCanceledException exception) when (lifetime.IsCancellationRequested && exception.CancellationToken == lifetime)
        {
            // Normal listener shutdown.
        }
    }
}
