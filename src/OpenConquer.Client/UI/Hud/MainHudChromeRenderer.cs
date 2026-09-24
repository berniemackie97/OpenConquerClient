using System.Runtime.ExceptionServices;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.UI.Hud;

internal sealed class MainHudChromeRenderer : IDisposable
{
    private readonly MainHudChromeLayout _layout;
    private readonly OpenGLTexture2D? _progressBackground;
    private readonly OpenGLTexture2D? _dialogFrame0;
    private readonly OpenGLTexture2D? _dialogFrame1;

    private bool _disposed;

    public MainHudChromeRenderer(OpenGLGraphicsDevice graphicsDevice, MainHudChromeAssets assets, LogicalRenderSize logicalRenderSize)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(assets);

        _layout = MainHudChromeLayout.Create(logicalRenderSize);

        OpenGLTexture2D? progressBackground = null;
        OpenGLTexture2D? dialogFrame0 = null;
        OpenGLTexture2D? dialogFrame1 = null;

        try
        {
            if (assets.ProgressBackground is { } progressImage)
            {
                progressBackground = graphicsDevice.CreateTexture2D(progressImage.Width, progressImage.Height, progressImage.Pixels.Span);
            }

            if (assets.HasDialogPanels)
            {
                dialogFrame0 = graphicsDevice.CreateTexture2D(assets.DialogFrame0!.Width, assets.DialogFrame0.Height, assets.DialogFrame0.Pixels.Span);
                dialogFrame1 = graphicsDevice.CreateTexture2D(assets.DialogFrame1!.Width, assets.DialogFrame1.Height, assets.DialogFrame1.Pixels.Span);
            }

            _progressBackground = progressBackground;
            _dialogFrame0 = dialogFrame0;
            _dialogFrame1 = dialogFrame1;
        }
        catch
        {
            DisposeCreatedTextures(dialogFrame1, dialogFrame0, progressBackground);
            throw;
        }
    }

    public void DrawBackground(OpenGLRenderer renderer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);

        if (_progressBackground is not null)
        {
            renderer.DrawSprite(_progressBackground, MainHudChromeLayout.BackgroundX, _layout.BackgroundY);
        }
    }

    public bool DrawPanels(OpenGLRenderer renderer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);

        if (_dialogFrame0 is null || _dialogFrame1 is null)
        {
            return false;
        }

        renderer.DrawSprite(_dialogFrame0, MainHudChromeLayout.DialogPanelASource, MainHudChromeLayout.DialogPanelAX, _layout.DialogPanelAY, MainHudChromeLayout.DialogPanelASource.Width, MainHudChromeLayout.DialogPanelASource.Height);
        renderer.DrawSprite(_dialogFrame0, MainHudChromeLayout.DialogPanelBSource, MainHudChromeLayout.DialogPanelBX, _layout.DialogPanelBY, MainHudChromeLayout.DialogPanelBSource.Width, MainHudChromeLayout.DialogPanelBSource.Height);
        renderer.DrawSprite(_dialogFrame1, MainHudChromeLayout.DialogPanelCSource, MainHudChromeLayout.DialogPanelCX, _layout.DialogPanelCY, MainHudChromeLayout.DialogPanelCSource.Width, MainHudChromeLayout.DialogPanelCSource.Height);
        renderer.DrawSprite(_dialogFrame1, MainHudChromeLayout.DialogPanelDSource, MainHudChromeLayout.DialogPanelDX, _layout.DialogPanelDY, MainHudChromeLayout.DialogPanelDSource.Width, MainHudChromeLayout.DialogPanelDSource.Height);

        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            _dialogFrame1?.Dispose();
        }
        catch (Exception exception)
        {
            firstFailure = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _dialogFrame0?.Dispose();
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _progressBackground?.Dispose();
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }
        finally
        {
            _disposed = true;
        }

        firstFailure?.Throw();
    }

    private static void DisposeCreatedTextures(OpenGLTexture2D? dialogFrame1, OpenGLTexture2D? dialogFrame0, OpenGLTexture2D? progressBackground)
    {
        try
        {
            dialogFrame1?.Dispose();
        }
        catch
        {
            // Preserve the texture-creation failure that initiated cleanup.
        }

        try
        {
            dialogFrame0?.Dispose();
        }
        catch
        {
            // Preserve the texture-creation failure that initiated cleanup.
        }

        try
        {
            progressBackground?.Dispose();
        }
        catch
        {
            // Preserve the texture-creation failure that initiated cleanup.
        }
    }
}
