using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Text;
using OpenConquer.Rendering.Sprites;
using OpenConquer.Rendering.Text.Rendering;

namespace OpenConquer.Client.UI.Login;

/// <summary>Non-retail but source-grounded account-list and BGM controls; owns graphics resources for one GL context.</summary>
internal sealed class LoginPanelControlsPresentation : IDisposable
{
    private static readonly SpriteColor s_fill = new(34, 25, 17, 225);
    private static readonly SpriteColor s_hover = new(78, 54, 27, 235);
    private static readonly SpriteColor s_border = new(160, 123, 66, 255);
    private static readonly SpriteColor s_enabled = new(244, 222, 176, 255);
    private static readonly SpriteColor s_disabled = new(114, 101, 83, 255);
    private static readonly NativeTextRenderOptions s_textStyle = new(NativeTextRenderStyle.Normal, s_enabled, s_enabled, 0, 0, NativeTextVertexColors.Solid(s_enabled));
    private static readonly NativeTextRenderOptions s_disabledStyle = new(NativeTextRenderStyle.Normal, s_disabled, s_disabled, 0, 0, NativeTextVertexColors.Solid(s_disabled));
    private readonly OpenGLGraphicsDevice _graphics;
    private readonly int _fontHeight;
    private readonly Dictionary<string, OpenGLTextLayout> _texts = new(StringComparer.Ordinal);
    private OpenGLTextContext? _font;
    private bool _disposed;

    public LoginPanelControlsPresentation(OpenGLGraphicsDevice graphics, LoginVisualLayout layout)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(layout);
        _graphics = graphics;
        _fontHeight = Math.Max(8, Scale(11, layout.Width));
    }

    public void Draw(OpenGLRenderer renderer, LoginPanelControls controls, LoginCredentialInput input)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(controls);
        ArgumentNullException.ThrowIfNull(input);
        for (int index = (int)LoginPanelControl.Save; index <= (int)LoginPanelControl.Music; index++)
            DrawButton(renderer, controls, input, (LoginPanelControl)index, selected: false);
        if (!input.Enabled)
            return;
        for (int index = 0; index < controls.VisibleAccountCount; index++)
            DrawButton(renderer, controls, input, (LoginPanelControl)((int)LoginPanelControl.Account0 + index), index == controls.SelectedIndex);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _texts.Clear();
        _font?.Dispose();
        _font = null;
    }

    private void DrawButton(OpenGLRenderer renderer, LoginPanelControls controls, LoginCredentialInput input, LoginPanelControl control, bool selected)
    {
        LoginSpritePlacement rect = controls.Bounds(control);
        bool enabled = controls.IsEnabled(control, input);
        bool hot = enabled && (controls.Hovered == control || selected);
        renderer.DrawSolidRectangle(rect.X, rect.Y, rect.Width, rect.Height, hot ? s_hover : s_fill);
        renderer.DrawLineRectangle(rect.X, rect.Y, rect.X + rect.Width - 1, rect.Y + rect.Height - 1, enabled ? s_border : s_disabled);
        string label = controls.Label(control);
        if (!_texts.TryGetValue(label, out OpenGLTextLayout? text))
        {
            _font ??= new OpenGLTextContext(_graphics, fontToken: null, nominalPixelHeight: _fontHeight, effectiveCodePage: 0, antialiasEnabled: true);
            text = _font.LayoutUnicode(label);
            if (!text.HasMissingGlyphs)
            {
                if (_texts.Count >= 32)
                    _texts.Clear();
                _texts.Add(label, text);
            }
        }
        if (text is not null && !text.HasMissingGlyphs)
        {
            int x = rect.X + Math.Max(2, (rect.Width - text.WidthPixels) / 2);
            int y = rect.Y + Math.Max(1, (rect.Height - _fontHeight) / 2);
            using OpenGLRenderer.ClipScope clip = renderer.Clip(rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2);
            _font!.Draw(renderer, text, enabled ? s_textStyle : s_disabledStyle, x, y);
        }
    }

    private static int Scale(int value, int width) => checked((int)(((long)value * width + 512) / 1024));
}
