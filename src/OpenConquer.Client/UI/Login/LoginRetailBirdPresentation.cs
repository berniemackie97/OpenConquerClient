using System.Diagnostics;
using OpenConquer.Content.Images;
using OpenConquer.Content.Startup;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Client.UI.Login;

/// <summary>Renders start.dat sprite 22 at its original root depth, using one offline-packed GPU atlas.</summary>
internal sealed class LoginRetailBirdPresentation : IDisposable
{
    private readonly OpenGLTexture2D _atlas;
    private readonly int _stageWidth;
    private readonly long _started;
    private bool _disposed;

    public LoginRetailBirdPresentation(OpenGLGraphicsDevice graphics, LoginVisualLayout layout)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(layout);
        RgbaImage pixels = LoginRetailMotionArtwork.LoadBirdAtlas();
        _atlas = graphics.CreateTexture2D(pixels.Width, pixels.Height, pixels.Pixels.Span);
        _stageWidth = layout.Width;
        _started = Stopwatch.GetTimestamp();
    }

    public void Draw(OpenGLRenderer renderer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        long tick = Stopwatch.GetElapsedTime(_started).Ticks * LoginRetailMotionArtwork.FramesPerSecond / TimeSpan.TicksPerSecond;
        int frame = (int)(tick % LoginRetailMotionArtwork.BirdFrameCount);
        LoginRetailMotionArtwork.BirdGlyph glyph = LoginRetailMotionArtwork.BirdAt(frame);
        if (!glyph.IsVisible)
        {
            return;
        }

        SpriteSourceRectangle source = new(glyph.AtlasX, glyph.AtlasY, glyph.Width, glyph.Height);
        renderer.DrawSprite(_atlas, source, Scale(glyph.StageX), Scale(glyph.StageY),
            Math.Max(1, Scale(glyph.Width)), Math.Max(1, Scale(glyph.Height)));
    }

    private int Scale(int coordinate) => checked((int)((coordinate * (long)_stageWidth + 512) / 1024));

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _atlas.Dispose();
    }
}
