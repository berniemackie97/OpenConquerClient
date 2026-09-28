using System.Runtime.ExceptionServices;
using OpenConquer.Content.Images;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.UI.Hud;

internal sealed class MainHudOrganiseButtonRenderer : IDisposable
{
    private readonly MainHudOrganiseButtonLayout _layout;
    private readonly OpenGLTexture2D? _frame0;
    private readonly OpenGLTexture2D? _frame1;
    private readonly OpenGLTexture2D? _frame2;
    private readonly OpenGLTexture2D? _frame3;

    private bool _disposed;

    public MainHudOrganiseButtonRenderer(OpenGLGraphicsDevice graphicsDevice, MainHudOrganiseButtonAssets assets, LogicalRenderSize logicalRenderSize)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(assets);

        _layout = MainHudOrganiseButtonLayout.Create(logicalRenderSize);

        OpenGLTexture2D? frame0 = null;
        OpenGLTexture2D? frame1 = null;
        OpenGLTexture2D? frame2 = null;
        OpenGLTexture2D? frame3 = null;

        try
        {
            if (assets.IsAvailable)
            {
                frame0 = CreateTexture(graphicsDevice, assets.GetFrame(0)!);
                frame1 = CreateTexture(graphicsDevice, assets.GetFrame(1)!);
                frame2 = CreateTexture(graphicsDevice, assets.GetFrame(2)!);
                frame3 = CreateTexture(graphicsDevice, assets.GetFrame(3)!);
            }

            _frame0 = frame0;
            _frame1 = frame1;
            _frame2 = frame2;
            _frame3 = frame3;
        }
        catch
        {
            DisposeCreatedTextures(frame3, frame2, frame1, frame0);
            throw;
        }
    }

    public void Draw(OpenGLRenderer renderer, MainHudOrganiseButtonState state)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(state);

        OpenGLTexture2D? texture = state.RenderFrame switch
        {
            MainHudOrganiseButtonState.NormalFrame => _frame0,
            MainHudOrganiseButtonState.PressedFrame => _frame1,
            MainHudOrganiseButtonState.DisabledFrame => _frame2,
            MainHudOrganiseButtonState.HoverFrame => _frame3,
            int frameIndex => throw new InvalidOperationException($"Unsupported Main3_OrganiseBtn frame {frameIndex}."),
        };

        if (texture is null)
        {
            return;
        }

        renderer.DrawSprite(texture, _layout.X, _layout.Y);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ExceptionDispatchInfo? firstFailure = null;

        DisposeTexture(_frame3, ref firstFailure);
        DisposeTexture(_frame2, ref firstFailure);
        DisposeTexture(_frame1, ref firstFailure);
        DisposeTexture(_frame0, ref firstFailure);

        _disposed = true;
        firstFailure?.Throw();
    }

    private static OpenGLTexture2D CreateTexture(OpenGLGraphicsDevice graphicsDevice, RgbaImage image)
    {
        return graphicsDevice.CreateTexture2D(image.Width, image.Height, image.Pixels.Span);
    }

    private static void DisposeTexture(OpenGLTexture2D? texture, ref ExceptionDispatchInfo? firstFailure)
    {
        try
        {
            texture?.Dispose();
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }
    }

    private static void DisposeCreatedTextures(params OpenGLTexture2D?[] textures)
    {
        foreach (OpenGLTexture2D? texture in textures)
        {
            try
            {
                texture?.Dispose();
            }
            catch
            {
                // Preserve the texture-creation failure that initiated cleanup.
            }
        }
    }
}
