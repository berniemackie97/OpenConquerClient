using OpenConquer.Content;
using OpenConquer.Content.Images;
using OpenConquer.Content.Login;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.OpenGL.Text;
using OpenConquer.Rendering.Sprites;
using OpenConquer.Rendering.Text.Rendering;

namespace OpenConquer.Client.UI.Login.Stage;

internal sealed class LoginLoadingPresentation : IDisposable
{
    private static readonly SpriteColor s_traceColor = new(178, 122, 45, 255);
    private static readonly NativeTextRenderOptions s_traceStyle = new(NativeTextRenderStyle.Normal,
        s_traceColor, s_traceColor, 0, 0, NativeTextVertexColors.Solid(s_traceColor));

    private static readonly string[] s_pictures =
    [
        "shapes_261", "shapes_375", "shapes_377", "sprite380", "shapes_383",
        "sprite387_f1", "sprite387_f2", "sprite387_f3", "sprite387_f4",
        "sprite387_f5", "sprite387_f6", "sprite387_f7",
        "shapes_389", "shapes_390", "shapes_391", "shapes_392",
    ];

    private readonly ResourceOwnership _ownership = new();
    private readonly LoginStageLayout _layout;
    private readonly LoginLoadingTimeline _timeline;
    private readonly Dictionary<string, OpenGLTexture2D> _pictures = new(StringComparer.Ordinal);
    private readonly OpenGLTextContext _traceFont;
    private OpenGLTextLayout? _traceLayout;
    private string? _lastTrace;

    internal LoginLoadingPresentation(OpenGLGraphicsDevice device, LoginStageLayout layout,
        IClientContentSource source, LoginLoadingTimeline timeline)
    {
        ArgumentNullException.ThrowIfNull(device);
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        ArgumentNullException.ThrowIfNull(source);
        _timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));

        try
        {
            foreach (string name in s_pictures)
            {
                RgbaImage image = TargaImageLoader.Load(source,
                    $"ui/loading/artwork/{name}.tga", ContentLookupMode.LooseOnly);
                _pictures.Add(name, _ownership.Track(device.CreateTexture2D(
                    image.Width, image.Height, image.Pixels.Span)));
            }

            _traceFont = _ownership.Track(new OpenGLTextContext(device, "Arial",
                Math.Max(1, layout.Scale(14)), 0, true));
        }
        catch
        {
            _ownership.DisposeAfterInitializationFailure();
            throw;
        }
    }

    internal void Draw(OpenGLRenderer renderer, LoginLoadingSequence sequence, string? trace)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(sequence);
        if (!sequence.Visible)
        {
            return;
        }

        Draw(renderer, "shapes_261", 0, -168, 1024, 1104);
        if (sequence.Frame >= 18)
        {
            (double fillX, double headX) = _timeline.PositionAt(sequence.Frame);
            Draw(renderer, "shapes_377", 0.45, 378, 1024, 12);
            Draw(renderer, "sprite380", 512 + fillX - 529.95, 319, 1042, 134);
            Draw(renderer, "shapes_383", 0, 288, 1024, 192);
            Draw(renderer, $"sprite387_f{sequence.HeadFrame + 1}", 512 + headX - 15, 359, 30, 50);
        }

        Draw(renderer, "shapes_375", 0, 337, 1024, 70);
        if (sequence.Frame >= 18)
        {
            int frame = sequence.LoadingTextFrame;
            Draw(renderer, $"shapes_{389 + frame}", 467.25, 432.95, 68 + frame * 4, 24);
        }

        if (sequence.Frame >= 17 && !string.IsNullOrEmpty(trace))
        {
            DrawTrace(renderer, trace);
        }
    }

    public void Dispose() => _ownership.Dispose();

    private void Draw(OpenGLRenderer renderer, string name, double x, double y, double width, double height)
    {
        LoginRect bounds = _layout.PlaceExact(x, y, width, height);
        renderer.DrawSprite(_pictures[name], bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    private void DrawTrace(OpenGLRenderer renderer, string trace)
    {
        if (!string.Equals(_lastTrace, trace, StringComparison.Ordinal))
        {
            _lastTrace = trace;
            _traceLayout = _traceFont.LayoutUnicode(trace);
        }

        LoginRect field = _layout.PlaceExact(86, 290, 856.1, 20.1);
        using OpenGLRenderer.ClipScope clip = renderer.Clip(field.X, field.Y, field.Width, field.Height);
        _traceFont.Draw(renderer, _traceLayout!, s_traceStyle,
            field.X + (field.Width - _traceLayout!.WidthPixels) / 2, field.Y + _layout.Scale(3));
    }
}
