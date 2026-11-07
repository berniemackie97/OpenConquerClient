using OpenConquer.Platform.Geometry;
using OpenConquer.Platform.Windowing.Desktop;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.World;

namespace OpenConquer.Client.World;

/// <summary>An admitted world owned by the client session, with context-scoped graphics released separately.</summary>
internal interface IClientWorld : IAsyncDisposable
{
    bool IsActive
    {
        get;
    }
    bool HasScene
    {
        get;
    }
    bool IsPresentable
    {
        get;
    }
    bool IsClosed
    {
        get;
    }
    bool TryObserveClose(out WorldCloseReason reason);
    void DrainIngress();
    void CompleteLoad();
    void RefreshAppearance(OpenGLGraphicsDevice? graphicsDevice);
    void PrepareFirstTextures(WorldMapTextureRenderer? textures, DesktopWindow window, OpenGLGraphicsDevice? graphicsDevice, OpenGLRenderer fallback);
    OpenGLRenderer GetRenderer(DesktopWindow window, OpenGLGraphicsDevice? graphicsDevice, OpenGLRenderer fallback);
    void PrepareFrame(WorldMapTextureRenderer? textures, OpenGLRenderer renderer);
    void DrawFrame(WorldMapTextureRenderer? textures, OpenGLRenderer renderer, DesktopWindow window);
    void HandlePointerMoved(PixelPoint point);
    void HandlePointerPressed(PixelPoint point);
    void HandlePointerReleased(PixelPoint point);
    void ResizeFramebuffer(PixelSize size);
    void ReleaseGraphics();
}
