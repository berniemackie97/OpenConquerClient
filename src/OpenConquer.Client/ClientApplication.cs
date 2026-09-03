using System.Runtime.ExceptionServices;
using OpenConquer.Content;
using OpenConquer.Content.Configuration;
using OpenConquer.Content.Startup;
using OpenConquer.Content.Wdf;
using OpenConquer.Platform;
using OpenConquer.Rendering;
using OpenConquer.Rendering.OpenGL;

namespace OpenConquer.Client;

internal sealed class ClientApplication : IDisposable
{
    private static readonly TimeSpan s_frameInterval = TimeSpan.FromMilliseconds(25);

    private readonly string _clientContentRootPath;
    private readonly PresentationPolicy _presentationPolicy;

    private OpenGLGraphicsDevice? _graphicsDevice;
    private OpenGLRenderer? _renderer;
    private DesktopWindow? _window;
    private LogicalRenderSize? _logicalRenderSize;
    private bool _runStarted;
    private bool _disposed;

    public ClientApplication(
        string clientContentRootPath,
        PresentationPolicy presentationPolicy = PresentationPolicy.Fit
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientContentRootPath);

        _clientContentRootPath = clientContentRootPath;
        _presentationPolicy = presentationPolicy;
    }

    public int Run()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_runStarted)
        {
            throw new InvalidOperationException("The client application has already been run.");
        }

        _runStarted = true;

        PackagedClientContentSource contentSource = PackagedClientContentSource.Open(
            _clientContentRootPath
        );

        ReportTolerableContentGaps(contentSource);

        // (timeGetTime() & 1) + 1 selects the retail variant; any monotonic millisecond counter
        // reproduces the parity, so the managed host uses its own tick source.
        StartupLogo startupLogo = StartupLogo.Load(contentSource, Environment.TickCount64);

        if (startupLogo.UnavailableReason is { } unavailableReason)
        {
            Console.Error.WriteLine($"OpenConquer: startup logo unavailable; {unavailableReason}");
        }

        DesktopWindow window = ClientWindowCreationSequence.CreateMainAfterStartup(
            new OpenGLStartupSplash(startupLogo),
            () => InitializeRuntimeConfiguration(contentSource),
            () => new DesktopWindow(s_frameInterval)
        );

        _window = window;

        window.FramebufferResized += OnFramebufferResized;
        window.Rendering += OnRendering;
        window.OpenGLContextReady += OnOpenGLContextReady;
        window.OpenGLContextReleasing += OnOpenGLContextReleasing;

        window.Run();

        return 0;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        DesktopWindow? window = _window;

        try
        {
            window?.Dispose();
        }
        finally
        {
            // GL-owned resources normally clear through OpenGLContextReleasing while the context is
            // current. If the platform cannot make the context current during a fatal teardown,
            // they must not be deleted afterward against a dead context. The terminal application
            // therefore releases its managed references without attempting an unsafe retry.
            _window = null;
            _renderer = null;
            _graphicsDevice = null;
            _disposed = true;
        }
    }

    private void OnOpenGLContextReady(IOpenGLContext context)
    {
        if (_graphicsDevice is not null || _renderer is not null)
        {
            throw new InvalidOperationException("OpenGL rendering has already been initialized.");
        }

        OpenGLGraphicsDevice graphicsDevice = new(context.GetProcAddress);

        try
        {
            DesktopWindow window =
                _window
                ?? throw new InvalidOperationException("The desktop window has not been created.");

            LogicalRenderSize logicalRenderSize =
                _logicalRenderSize
                ?? throw new InvalidOperationException(
                    "The logical render size has not been initialized."
                );

            PixelSize framebufferSize = window.FramebufferSize;

            OpenGLRenderer renderer = graphicsDevice.CreateRenderer(
                logicalRenderSize,
                framebufferSize.Width,
                framebufferSize.Height,
                _presentationPolicy
            );

            _renderer = renderer;
            _graphicsDevice = graphicsDevice;
        }
        catch
        {
            try
            {
                graphicsDevice.Dispose();
            }
            catch
            {
                // Preserve the renderer/context initialization failure.
            }

            throw;
        }
    }

    private void OnFramebufferResized(PixelSize size)
    {
        _renderer?.ResizeHostFramebuffer(size.Width, size.Height);
    }

    /// <summary>
    /// Reports the <c>ini/package.ini</c> declarations retail resolves without failing.
    /// </summary>
    /// <remarks>
    /// A missing or duplicate declaration is not an error: the native reader returns
    /// <see langword="void"/> and its caller discards <c>TqPackagesOpen</c>'s result at
    /// <c>0x1001A406</c>. Reporting keeps an incomplete content set visible instead of silent.
    /// </remarks>
    private static void ReportTolerableContentGaps(PackagedClientContentSource contentSource)
    {
        foreach (WdfPackageRegistration registration in contentSource.PackageRegistrations)
        {
            if (registration.Outcome == WdfPackageRegistrationOutcome.Registered)
            {
                continue;
            }

            Console.Error.WriteLine(
                $"OpenConquer: declared package '{registration.DeclaredName}' "
                    + $"(prefix '{registration.Prefix}') was not registered: "
                    + $"{registration.Outcome}."
            );
        }
    }

    private void InitializeRuntimeConfiguration(IClientContentSource contentSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        GameSetupConfiguration gameSetup = GameSetupConfiguration.Load(contentSource);

        _logicalRenderSize = new LogicalRenderSize(
            gameSetup.LogicalWidthPixels,
            gameSetup.LogicalHeightPixels
        );
    }

    private void OnRendering(double _)
    {
        _renderer?.RenderFrame();
    }

    private void OnOpenGLContextReleasing()
    {
        ReleaseRenderingResources();
    }

    private void ReleaseRenderingResources()
    {
        OpenGLRenderer? renderer = _renderer;
        OpenGLGraphicsDevice? graphicsDevice = _graphicsDevice;

        // Clear ownership before invoking user/driver cleanup so re-entrant teardown cannot attempt
        // the same OpenGL resources twice.
        _renderer = null;
        _graphicsDevice = null;

        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            renderer?.Dispose();
        }
        catch (Exception exception)
        {
            firstFailure = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            graphicsDevice?.Dispose();
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        firstFailure?.Throw();
    }
}
