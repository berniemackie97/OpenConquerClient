using System.Diagnostics;
using System.Runtime.ExceptionServices;
using OpenConquer.Content.Images;
using OpenConquer.Content.Startup;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;

namespace OpenConquer.Client.UI.Login;

/// <summary>One-shot playback of the 27 original loginFlash frames, owned by one OpenGL context.</summary>
internal sealed class LoginRetailOpeningPresentation : IDisposable
{
    private readonly OpenGLTexture2D?[] _frames = new OpenGLTexture2D?[LoginRetailMotionArtwork.OpeningFrameCount];
    private readonly List<OpenGLTexture2D> _textures = [];
    private readonly int _stageWidth;
    private readonly int _stageHeight;
    private readonly long _started;
    private bool _released;

    public LoginRetailOpeningPresentation(OpenGLGraphicsDevice graphics, LoginVisualLayout layout)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(layout);
        _stageWidth = layout.Width;
        _stageHeight = layout.Height;
        try
        {
            Dictionary<string, OpenGLTexture2D> unique = new(StringComparer.Ordinal);
            for (int frame = 0; frame < _frames.Length; frame++)
            {
                string digest = LoginRetailMotionArtwork.IntroDigest(frame);
                if (!unique.TryGetValue(digest, out OpenGLTexture2D? texture))
                {
                    RgbaImage image = LoginRetailMotionArtwork.LoadOpening(frame);
                    texture = graphics.CreateTexture2D(image.Width, image.Height, image.Pixels.Span);
                    _textures.Add(texture);
                    unique.Add(digest, texture);
                }
                _frames[frame] = texture;
            }
            _started = Stopwatch.GetTimestamp();
        }
        catch
        {
            try
            {
                Release();
            }
            catch { /* Preserve the original OpenGL initialization exception. */ }
            throw;
        }
    }

    public bool IsPlaying => !_released && CurrentFrame < LoginRetailMotionArtwork.OpeningFrameCount;

    private int CurrentFrame
    {
        get
        {
            long frame = Stopwatch.GetElapsedTime(_started).Ticks * LoginRetailMotionArtwork.FramesPerSecond / TimeSpan.TicksPerSecond;
            return (int)Math.Min(frame, LoginRetailMotionArtwork.OpeningFrameCount);
        }
    }

    public void Draw(OpenGLRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        if (_released)
        {
            return;
        }

        int frame = CurrentFrame;
        if (frame >= _frames.Length)
        {
            Release();
            return;
        }
        renderer.DrawSprite(_frames[frame]!, 0, 0, _stageWidth, _stageHeight);
    }

    public void Dispose() => Release();

    private void Release()
    {
        if (_released)
        {
            return;
        }

        _released = true;
        ExceptionDispatchInfo? first = null;
        foreach (OpenGLTexture2D texture in _textures)
        {
            try
            {
                texture.Dispose();
            }
            catch (Exception exception) { first ??= ExceptionDispatchInfo.Capture(exception); }
        }
        _textures.Clear();
        Array.Clear(_frames);
        first?.Throw();
    }
}
