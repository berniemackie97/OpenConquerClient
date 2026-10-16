using System.Runtime.ExceptionServices;
using OpenConquer.Content.Images;
using OpenConquer.Content.Startup;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.OpenGL.Text;
using OpenConquer.Rendering.Sprites;
using OpenConquer.Rendering.Text.Rendering;

namespace OpenConquer.Client.UI.Login;

/// <summary>Original 5517 account-row skins and frame-stopped confirmation movies.</summary>
internal sealed class LoginRetailPanelPresentation : IDisposable
{
    private static readonly SpriteColor s_accountColor = new(175, 58, 58, 255); // DefineEditText 4
    private static readonly SpriteColor s_confirmationColor = new(255, 70, 66, 255); // DefineEditText 284
    private static readonly NativeTextRenderOptions s_accountText = Style(s_accountColor);
    private static readonly NativeTextRenderOptions s_confirmationText = Style(s_confirmationColor);

    private readonly OpenGLTexture2D?[,] _dialogFrames = new OpenGLTexture2D?[2, 10];
    private readonly List<OpenGLTexture2D> _ownedDialogTextures = [];
    private readonly OpenGLTexture2D[] _rows = new OpenGLTexture2D[2];
    private readonly OpenGLTexture2D?[,] _dialogButtons = new OpenGLTexture2D?[2, 2];
    private readonly OpenGLGraphicsDevice _graphics;
    private readonly int _stageWidth;
    private readonly OpenGLTextLayout?[] _labels = new OpenGLTextLayout?[LoginAccountHistory.Capacity];
    private readonly string?[] _names = new string?[LoginAccountHistory.Capacity];
    private OpenGLTextContext? _font;
    private string? _confirmationName;
    private OpenGLTextLayout? _confirmationLayout;
    private bool _disposed;

    public LoginRetailPanelPresentation(OpenGLGraphicsDevice graphics, int stageWidth)
    {
        _graphics = graphics ?? throw new ArgumentNullException(nameof(graphics));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(stageWidth);
        _stageWidth = stageWidth;
        try
        {
            // Ten native frames contain repeated show/hide compositions. Share identical
            // pixel-verified textures, rather than allocating 16 full-stage RGBA images.
            Dictionary<string, OpenGLTexture2D> unique = new(StringComparer.Ordinal);
            for (int kind = 0; kind < 2; kind++)
                for (int frame = 2; frame <= 9; frame++)
                {
                    string digest = LoginRetailPanelArtwork.DialogDigest(kind, frame);
                    if (!unique.TryGetValue(digest, out OpenGLTexture2D? texture))
                    {
                        RgbaImage image = LoginRetailPanelArtwork.LoadDialog(kind, frame);
                        texture = graphics.CreateTexture2D(image.Width, image.Height, image.Pixels.Span);
                        _ownedDialogTextures.Add(texture);
                        unique.Add(digest, texture);
                    }
                    _dialogFrames[kind, frame] = texture;
                }
            for (int state = 0; state < 2; state++)
            {
                RgbaImage image = LoginRetailPanelArtwork.LoadRow(highlighted: state != 0);
                _rows[state] = graphics.CreateTexture2D(image.Width, image.Height, image.Pixels.Span);
            }
            for (int button = 0; button < 2; button++)
                for (int state = 1; state <= 2; state++)
                {
                    RgbaImage image = LoginRetailPanelArtwork.LoadDialogButton(button, state);
                    _dialogButtons[button, state - 1] = graphics.CreateTexture2D(image.Width, image.Height, image.Pixels.Span);
                }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Draw(OpenGLRenderer renderer, LoginRetailPanelControls controls)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(controls);
        DrawHistory(renderer, controls);
        if (controls.Dialog != LoginRetailDialog.None)
            DrawDialog(renderer, controls);
    }

    private void DrawHistory(OpenGLRenderer renderer, LoginRetailPanelControls controls)
    {
        if (controls.Popup == LoginAccountPopupState.Closed)
            return;
        int count = controls.VisibleRowCount;
        int left = Scale(452);
        int top = Scale(631 - 25 * count);
        int width = Scale(166);
        int bottom = Scale(631);
        if (width <= 0 || bottom <= top)
            return;
        EnsureLabels(controls.SavedAccounts);
        using OpenGLRenderer.ClipScope clip = renderer.Clip(left, top, width, bottom - top);
        for (int i = 0; i < count; i++)
        {
            LoginSpritePlacement bounds = controls.RowBounds(i);
            OpenGLTexture2D row = _rows[controls.HoveredRow == i ? 1 : 0];
            renderer.DrawSprite(row, bounds.X, bounds.Y, bounds.Width, bounds.Height);
            if (_labels[i] is { } label)
            {
                // start.dat row itemBtn's 14-pixel Arial device text, horizontally inset.
                _font!.Draw(renderer, label, s_accountText, bounds.X + Scale(6), bounds.Y + Scale(3));
            }
        }
    }

    private void DrawDialog(OpenGLRenderer renderer, LoginRetailPanelControls controls)
    {
        int kind = controls.Dialog == LoginRetailDialog.Save ? 0 : 1;
        int frame = controls.DialogFrame;
        if (frame is < 2 or > 9)
            return;
        OpenGLTexture2D? image = _dialogFrames[kind, frame];
        if (image is null)
            return;
        renderer.DrawSprite(image, 0, 0, Scale(1024), Scale(768));
        if (frame == 6)
        {
            for (int button = 0; button < 2; button++)
            {
                int state = (int)controls.DialogButtonState(button);
                if (state == 0)
                    continue; // Source frame already includes the genuine Up state.
                OpenGLTexture2D overlay = _dialogButtons[button, state - 1]!;
                renderer.DrawSprite(overlay, Scale(button == 0 ? 381 : 541), Scale(415), Scale(96), Scale(26));
            }
        }
        if (kind == 1 && frame >= 6 && controls.DialogAccount is { } account)
        {
            // Retail undefined sDelID_0/1: exact runtime text is leading space,
            // quoted account, trailing space (field 284, #FF4642, Arial 14).
            string text = $" \"{account}\" ";
            if (_confirmationName != text)
            {
                _confirmationName = text;
                _confirmationLayout = GetFont().LayoutUnicode(text);
            }
            if (_confirmationLayout is { HasMissingGlyphs: false } label)
                GetFont().Draw(renderer, label, s_confirmationText, Scale(364), Scale(291));
        }
    }

    private void EnsureLabels(IReadOnlyList<string> accounts)
    {
        OpenGLTextContext font = GetFont();
        for (int i = 0; i < accounts.Count; i++)
        {
            if (accounts[i] == _names[i])
                continue;
            _names[i] = accounts[i];
            _labels[i] = font.LayoutUnicode(accounts[i]);
        }
        for (int i = accounts.Count; i < _labels.Length; i++)
        {
            _names[i] = null;
            _labels[i] = null;
        }
    }

    private OpenGLTextContext GetFont() => _font ??= new OpenGLTextContext(_graphics, fontToken: null,
        nominalPixelHeight: Math.Max(1, Scale(14)), effectiveCodePage: 0, antialiasEnabled: true);

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        ExceptionDispatchInfo? error = null;
        try
        {
            _font?.Dispose();
        }
        catch (Exception ex) { error = ExceptionDispatchInfo.Capture(ex); }
        foreach (OpenGLTexture2D texture in _ownedDialogTextures)
            DisposeTexture(texture, ref error);
        _ownedDialogTextures.Clear();
        foreach (OpenGLTexture2D? texture in _rows)
            DisposeTexture(texture, ref error);
        for (int button = 0; button < 2; button++)
            for (int state = 0; state < 2; state++)
                DisposeTexture(_dialogButtons[button, state], ref error);
        error?.Throw();
    }

    private static void DisposeTexture(OpenGLTexture2D? texture, ref ExceptionDispatchInfo? error)
    {
        try
        {
            texture?.Dispose();
        }
        catch (Exception ex) { error ??= ExceptionDispatchInfo.Capture(ex); }
    }

    private int Scale(int value) => checked((int)((value * (long)_stageWidth + 512) / 1024));
    private static NativeTextRenderOptions Style(SpriteColor color) =>
        new(NativeTextRenderStyle.Normal, color, color, 0, 0, NativeTextVertexColors.Solid(color));
}
