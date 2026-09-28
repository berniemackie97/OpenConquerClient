using System.Runtime.ExceptionServices;
using OpenConquer.Client.Startup;
using OpenConquer.Client.UI.Hud;
using OpenConquer.Content;
using OpenConquer.Content.Configuration;
using OpenConquer.Content.Startup;
using OpenConquer.Content.Wdf;
using OpenConquer.Platform.Geometry;
using OpenConquer.Platform.OpenGL;
using OpenConquer.Platform.Windowing.Desktop;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client;

internal sealed class ClientApplication : IDisposable
{
    private static readonly TimeSpan s_frameInterval = TimeSpan.FromMilliseconds(25);

    private readonly string _contentRootPath;
    private readonly PresentationPolicy _presentationPolicy;
    private readonly DesktopWindowMode _windowMode;
    private readonly PixelSize _windowSize;
    private readonly MainHudVitalsState _mainHudVitalsState = new();
    private readonly MainHudSkillExperienceState _mainHudSkillExperienceState = new();
    private readonly MainHudOrganiseButtonState _mainHudOrganiseButtonState = new();

    private MainHudChromeAssets? _mainHudChromeAssets;
    private MainHudVitalsAssets? _mainHudVitalsAssets;
    private MainHudSkillAssets? _mainHudSkillAssets;
    private MainHudOrganiseButtonAssets? _mainHudOrganiseButtonAssets;
    private MainHudChromeRenderer? _mainHudChromeRenderer;
    private MainHudVitalsRenderer? _mainHudVitalsRenderer;
    private MainHudSkillExperienceRenderer? _mainHudSkillExperienceRenderer;
    private MainHudOrganiseButtonRenderer? _mainHudOrganiseButtonRenderer;
    private MainHudOrganiseButtonLayout? _mainHudOrganiseButtonLayout;
    private OpenGLGraphicsDevice? _graphicsDevice;
    private OpenGLRenderer? _renderer;
    private DesktopWindow? _window;
    private LogicalRenderSize? _logicalRenderSize;
    private bool _runStarted;
    private bool _disposed;

    public ClientApplication(string contentRootPath, PresentationPolicy presentationPolicy = PresentationPolicy.Fit, DesktopWindowMode windowMode = DesktopWindowMode.Resizable, PixelSize? windowSize = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);

        if (!Enum.IsDefined(windowMode))
        {
            throw new ArgumentOutOfRangeException(nameof(windowMode), windowMode, "Unsupported desktop window mode.");
        }

        _contentRootPath = contentRootPath;
        _presentationPolicy = presentationPolicy;
        _windowMode = windowMode;

        PixelSize configuredWindowSize = windowSize ?? DesktopWindow.DefaultWindowSize;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(configuredWindowSize.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(configuredWindowSize.Height);
        _windowSize = configuredWindowSize;
    }

    public int Run()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_runStarted)
        {
            throw new InvalidOperationException("The client application has already been run.");
        }

        _runStarted = true;

        PackagedClientContentSource contentSource = PackagedClientContentSource.Open(_contentRootPath);

        ReportPackageRegistrationWarnings(contentSource);

        StartupLogo startupLogo = StartupLogo.Load(contentSource, Environment.TickCount64);

        if (startupLogo.UnavailableReason is { } unavailableReason)
        {
            Console.Error.WriteLine($"OpenConquer: startup logo unavailable; {unavailableReason}");
        }

        DesktopWindow window = StartupWindowSequence.CreateMainAfterStartup(new OpenGLStartupSplash(startupLogo), () => InitializeRuntimeConfiguration(contentSource), () => new DesktopWindow(_windowSize, _windowMode, s_frameInterval));

        _window = window;

        window.FramebufferResized += OnFramebufferResized;
        window.PointerMoved += OnPointerMoved;
        window.PrimaryPointerPressed += OnPrimaryPointerPressed;
        window.PrimaryPointerReleased += OnPrimaryPointerReleased;
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
            _window = null;
            _mainHudOrganiseButtonRenderer = null;
            _mainHudSkillExperienceRenderer = null;
            _mainHudVitalsRenderer = null;
            _mainHudChromeRenderer = null;
            _mainHudOrganiseButtonLayout = null;
            _mainHudOrganiseButtonAssets = null;
            _mainHudSkillAssets = null;
            _mainHudVitalsAssets = null;
            _mainHudChromeAssets = null;
            _renderer = null;
            _graphicsDevice = null;
            _disposed = true;
        }
    }

    private void OnOpenGLContextReady(IOpenGLContext context)
    {
        if (_graphicsDevice is not null ||
            _renderer is not null ||
            _mainHudChromeRenderer is not null ||
            _mainHudVitalsRenderer is not null ||
            _mainHudSkillExperienceRenderer is not null ||
            _mainHudOrganiseButtonRenderer is not null)
        {
            throw new InvalidOperationException("OpenGL rendering has already been initialized.");
        }

        OpenGLGraphicsDevice graphicsDevice = new(context.GetProcAddress);
        OpenGLRenderer? renderer = null;
        MainHudChromeRenderer? mainHudChromeRenderer = null;
        MainHudVitalsRenderer? mainHudVitalsRenderer = null;
        MainHudSkillExperienceRenderer? mainHudSkillExperienceRenderer = null;
        MainHudOrganiseButtonRenderer? mainHudOrganiseButtonRenderer = null;

        try
        {
            DesktopWindow window = _window ?? throw new InvalidOperationException("The desktop window has not been created.");
            LogicalRenderSize logicalRenderSize = _logicalRenderSize ?? throw new InvalidOperationException("The logical render size has not been initialized.");
            MainHudChromeAssets mainHudChromeAssets = _mainHudChromeAssets ?? throw new InvalidOperationException("The main HUD chrome assets have not been initialized.");
            MainHudVitalsAssets mainHudVitalsAssets = _mainHudVitalsAssets ?? throw new InvalidOperationException("The main HUD vitals assets have not been initialized.");
            MainHudSkillAssets mainHudSkillAssets = _mainHudSkillAssets ?? throw new InvalidOperationException("The main HUD skill assets have not been initialized.");
            MainHudOrganiseButtonAssets mainHudOrganiseButtonAssets = _mainHudOrganiseButtonAssets ?? throw new InvalidOperationException("The main HUD organise-button assets have not been initialized.");
            PixelSize framebufferSize = window.FramebufferSize;

            renderer = graphicsDevice.CreateRenderer(logicalRenderSize, framebufferSize.Width, framebufferSize.Height, _presentationPolicy);
            mainHudChromeRenderer = new MainHudChromeRenderer(graphicsDevice, mainHudChromeAssets, logicalRenderSize);
            mainHudVitalsRenderer = new MainHudVitalsRenderer(graphicsDevice, mainHudVitalsAssets, logicalRenderSize);
            mainHudSkillExperienceRenderer = new MainHudSkillExperienceRenderer(graphicsDevice, mainHudSkillAssets, logicalRenderSize);
            mainHudOrganiseButtonRenderer = new MainHudOrganiseButtonRenderer(graphicsDevice, mainHudOrganiseButtonAssets, logicalRenderSize);

            _renderer = renderer;
            _mainHudChromeRenderer = mainHudChromeRenderer;
            _mainHudVitalsRenderer = mainHudVitalsRenderer;
            _mainHudSkillExperienceRenderer = mainHudSkillExperienceRenderer;
            _mainHudOrganiseButtonRenderer = mainHudOrganiseButtonRenderer;
            _graphicsDevice = graphicsDevice;
        }
        catch
        {
            try
            {
                mainHudOrganiseButtonRenderer?.Dispose();
            }
            catch
            {
                // Preserve the renderer/context initialization failure.
            }

            try
            {
                mainHudSkillExperienceRenderer?.Dispose();
            }
            catch
            {
                // Preserve the renderer/context initialization failure.
            }

            try
            {
                mainHudVitalsRenderer?.Dispose();
            }
            catch
            {
                // Preserve the renderer/context initialization failure.
            }

            try
            {
                mainHudChromeRenderer?.Dispose();
            }
            catch
            {
                // Preserve the renderer/context initialization failure.
            }

            try
            {
                renderer?.Dispose();
            }
            catch
            {
                // Preserve the renderer/context initialization failure.
            }

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

    private void OnPointerMoved(PixelPoint point)
    {
        if (!CanInteractWithOrganiseButton() || _mainHudOrganiseButtonLayout is not { } layout)
        {
            return;
        }

        if (TryMapPointerToLogical(point, out int logicalX, out int logicalY))
        {
            _mainHudOrganiseButtonState.HandlePointerMoved(logicalX, logicalY, layout);
        }
        else
        {
            _mainHudOrganiseButtonState.HandlePointerMoved(-1, -1, layout);
        }
    }

    private void OnPrimaryPointerPressed(PixelPoint point)
    {
        if (!CanInteractWithOrganiseButton() ||
            _mainHudOrganiseButtonLayout is not { } layout ||
            !TryMapPointerToLogical(point, out int logicalX, out int logicalY))
        {
            return;
        }

        _mainHudOrganiseButtonState.HandleLeftButtonDown(logicalX, logicalY, layout);
    }

    private void OnPrimaryPointerReleased(PixelPoint point)
    {
        if (!CanInteractWithOrganiseButton() || _mainHudOrganiseButtonLayout is not { } layout)
        {
            return;
        }

        if (TryMapPointerToLogical(point, out int logicalX, out int logicalY))
        {
            _mainHudOrganiseButtonState.HandleLeftButtonUp(logicalX, logicalY, layout, out _);
        }
        else
        {
            _mainHudOrganiseButtonState.HandleLeftButtonUp(-1, -1, layout, out _);
        }
    }

    private bool TryMapPointerToLogical(PixelPoint point, out int logicalX, out int logicalY)
    {
        OpenGLRenderer? renderer = _renderer;

        if (renderer is null)
        {
            logicalX = 0;
            logicalY = 0;
            return false;
        }

        return renderer.Viewport.TryMapPointerToLogical(point.X, point.Y, out logicalX, out logicalY);
    }

    private bool CanInteractWithOrganiseButton()
    {
        return _mainHudChromeAssets?.HasDialogPanels == true &&
            _mainHudOrganiseButtonAssets?.IsAvailable == true;
    }

    private static void ReportPackageRegistrationWarnings(PackagedClientContentSource contentSource)
    {
        foreach (WdfPackageRegistration registration in contentSource.PackageRegistrations)
        {
            if (registration.Outcome == WdfPackageRegistrationOutcome.Registered)
            {
                continue;
            }

            Console.Error.WriteLine($"OpenConquer: declared package '{registration.DeclaredName}' (prefix '{registration.Prefix}') resolved as {registration.Outcome}.");
        }
    }

    private void InitializeRuntimeConfiguration(IClientContentSource contentSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        GameSetupConfiguration gameSetup = GameSetupConfiguration.Load(contentSource);
        LogicalRenderSize logicalRenderSize = new(gameSetup.LogicalWidthPixels, gameSetup.LogicalHeightPixels);

        _logicalRenderSize = logicalRenderSize;
        _mainHudChromeAssets = MainHudChromeAssets.Load(contentSource);
        _mainHudVitalsAssets = MainHudVitalsAssets.Load(contentSource);
        _mainHudSkillAssets = MainHudSkillAssets.Load(contentSource);
        _mainHudOrganiseButtonAssets = MainHudOrganiseButtonAssets.Load(contentSource);
        _mainHudOrganiseButtonLayout = MainHudOrganiseButtonLayout.Create(logicalRenderSize);
    }

    private void OnRendering(double elapsedSeconds)
    {
        OpenGLRenderer? renderer = _renderer;

        if (renderer is null)
        {
            return;
        }

        renderer.BeginFrame();

        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            _mainHudChromeRenderer?.DrawBackground(renderer);
            _mainHudVitalsRenderer?.Draw(renderer, _mainHudVitalsState);

            if (_mainHudChromeRenderer is { } mainHudChromeRenderer && mainHudChromeRenderer.DrawPanels(renderer))
            {
                _mainHudSkillExperienceRenderer?.Draw(renderer, _mainHudSkillExperienceState);
                _mainHudOrganiseButtonRenderer?.Draw(renderer, _mainHudOrganiseButtonState);
                _mainHudSkillExperienceState.AdvanceAfterHudDraw(unchecked((uint)Environment.TickCount64));
            }
        }
        catch (Exception exception)
        {
            firstFailure = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            renderer.EndFrame();
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        firstFailure?.Throw();
    }

    private void OnOpenGLContextReleasing()
    {
        ReleaseRenderingResources();
    }

    private void ReleaseRenderingResources()
    {
        MainHudOrganiseButtonRenderer? mainHudOrganiseButtonRenderer = _mainHudOrganiseButtonRenderer;
        MainHudSkillExperienceRenderer? mainHudSkillExperienceRenderer = _mainHudSkillExperienceRenderer;
        MainHudVitalsRenderer? mainHudVitalsRenderer = _mainHudVitalsRenderer;
        MainHudChromeRenderer? mainHudChromeRenderer = _mainHudChromeRenderer;
        OpenGLRenderer? renderer = _renderer;
        OpenGLGraphicsDevice? graphicsDevice = _graphicsDevice;

        _mainHudOrganiseButtonRenderer = null;
        _mainHudSkillExperienceRenderer = null;
        _mainHudVitalsRenderer = null;
        _mainHudChromeRenderer = null;
        _renderer = null;
        _graphicsDevice = null;

        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            mainHudOrganiseButtonRenderer?.Dispose();
        }
        catch (Exception exception)
        {
            firstFailure = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            mainHudSkillExperienceRenderer?.Dispose();
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            mainHudVitalsRenderer?.Dispose();
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            mainHudChromeRenderer?.Dispose();
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            renderer?.Dispose();
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
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
