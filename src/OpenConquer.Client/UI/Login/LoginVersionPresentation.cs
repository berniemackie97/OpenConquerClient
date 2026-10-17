using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Text;
using OpenConquer.Rendering.Sprites;
using OpenConquer.Rendering.Text.Rendering;

namespace OpenConquer.Client.UI.Login;

/// <summary>Root depth 276 <c>verTxt</c>: edit text 8, device Arial Bold 14, #888888, left aligned; shown from root frame 3.</summary>
internal sealed class LoginVersionPresentation : IDisposable
{
    private static readonly SpriteColor s_color = new(0x88, 0x88, 0x88, 255);
    private static readonly NativeTextRenderOptions s_style = new(NativeTextRenderStyle.Normal, s_color, s_color, 0, 0, NativeTextVertexColors.Solid(s_color));
    private readonly OpenGLTextContext _text;
    private readonly OpenGLTextLayout _layout;
    private readonly LoginSpritePlacement _field;
    private bool _disposed;

    public LoginVersionPresentation(OpenGLGraphicsDevice graphicsDevice, LoginVisualLayout layout, string label)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(label);

        _field = layout.VersionLabel;
        _text = new OpenGLTextContext(graphicsDevice, "Arial", Math.Max(1, layout.Scale(14)), effectiveCodePage: 0, antialiasEnabled: true, bold: true);
        _layout = _text.LayoutUnicode(label);
    }

    public void Draw(OpenGLRenderer renderer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);

        using OpenGLRenderer.ClipScope clip = renderer.Clip(_field.X, _field.Y, _field.Width, _field.Height);
        _text.Draw(renderer, _layout, s_style, _field.X, _field.Y);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _text.Dispose();
    }
}
