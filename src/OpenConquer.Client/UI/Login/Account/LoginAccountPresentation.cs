using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Content.Images;
using OpenConquer.Content.Login;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.OpenGL.Text;
using OpenConquer.Rendering.Sprites;
using OpenConquer.Rendering.Text.Rendering;

namespace OpenConquer.Client.UI.Login.Account;

/// <summary>Draws the saved-account list and the confirmation dialogs for one OpenGL context.</summary>
internal sealed class LoginAccountPresentation : IDisposable
{
    // itemBtn edit text 4 and clearMC edit text 284: device Arial 14.
    private const int FontHeight = 14;
    private static readonly NativeTextRenderOptions s_rowText = Style(new SpriteColor(0xAF, 0x3A, 0x3A, 255));
    private static readonly NativeTextRenderOptions s_messageText = Style(new SpriteColor(0xFF, 0x46, 0x42, 255));

    private readonly ResourceOwnership _ownership = new();
    private readonly LoginAccountLayout _layout;
    private readonly OpenGLTexture2D[] _dialogs = new OpenGLTexture2D[2];
    private readonly OpenGLTexture2D[] _rows = new OpenGLTexture2D[2];
    private readonly OpenGLTexture2D[,] _dialogButtons = new OpenGLTexture2D[2, 2];
    private readonly OpenGLTextContext _font;
    private readonly OpenGLTextLayout?[] _rowLabels = new OpenGLTextLayout?[LoginAccountHistory.Capacity];
    private readonly string?[] _rowNames = new string?[LoginAccountHistory.Capacity];
    private string? _messageAccount;
    private OpenGLTextLayout? _message;

    public LoginAccountPresentation(OpenGLGraphicsDevice graphicsDevice, LoginAccountLayout layout)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));

        try
        {
            foreach (LoginAccountDialog dialog in Enum.GetValues<LoginAccountDialog>())
            {
                _dialogs[(int)dialog] = Create(graphicsDevice, LoginAccountArtwork.LoadDialog(dialog));
            }

            _rows[0] = Create(graphicsDevice, LoginAccountArtwork.LoadRow(highlighted: false));
            _rows[1] = Create(graphicsDevice, LoginAccountArtwork.LoadRow(highlighted: true));

            foreach (LoginDialogButton button in Enum.GetValues<LoginDialogButton>())
            {
                _dialogButtons[(int)button, 0] = Create(graphicsDevice, LoginAccountArtwork.LoadDialogButton(button, LoginButtonState.Over));
                _dialogButtons[(int)button, 1] = Create(graphicsDevice, LoginAccountArtwork.LoadDialogButton(button, LoginButtonState.Down));
            }

            _font = _ownership.Track(new OpenGLTextContext(graphicsDevice, "Arial", Math.Max(1, layout.Scale(FontHeight)),
                effectiveCodePage: 0, antialiasEnabled: true));
        }
        catch
        {
            _ownership.DisposeAfterInitializationFailure();
            throw;
        }
    }

    public void Draw(OpenGLRenderer renderer, LoginAccountControls controls)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(controls);

        if (controls.List != LoginAccountList.Closed)
        {
            DrawList(renderer, controls);
        }

        if (controls.Dialog is { } dialog)
        {
            DrawDialog(renderer, controls, dialog);
        }
    }

    public void Dispose() => _ownership.Dispose();

    private void DrawList(OpenGLRenderer renderer, LoginAccountControls controls)
    {
        IReadOnlyList<string> accounts = controls.History.Accounts;
        UpdateRowLabels(accounts);

        // listMC is masked to the area above its anchor while it slides.
        LoginRect clip = _layout.ListClip(controls.VisibleRowCount);
        using OpenGLRenderer.ClipScope scope = renderer.Clip(clip.X, clip.Y, clip.Width, clip.Height);
        for (int row = 0; row < controls.VisibleRowCount; row++)
        {
            LoginRect bounds = controls.RowBounds(row);
            renderer.DrawSprite(_rows[row == controls.HoveredRow ? 1 : 0], bounds.X, bounds.Y, bounds.Width, bounds.Height);
            if (_rowLabels[row] is { } label)
            {
                // nameTxt sits at (6, 3) inside its row.
                _font.Draw(renderer, label, s_rowText, bounds.X + _layout.Scale(6), bounds.Y + _layout.Scale(3));
            }
        }
    }

    private void DrawDialog(OpenGLRenderer renderer, LoginAccountControls controls, LoginAccountDialog dialog)
    {
        int frame = controls.DialogFrame;
        int alpha = LoginAccountArtwork.MaskAlpha(frame) * 255 / 256;
        SpriteColor mask = new(0, 0, 0, (byte)alpha);
        LoginRect band = _layout.Mask;

        if (frame != LoginAccountArtwork.DialogHeldFrame)
        {
            if (alpha > 0)
            {
                renderer.DrawSolidRectangle(band.X, band.Y, band.Width, band.Height, mask);
            }

            return;
        }

        // The held dialog picture already carries the mask beneath it; draw the mask only around it.
        OpenGLTexture2D picture = _dialogs[(int)dialog];
        LoginRect area = _layout.Dialog(dialog, picture.Width, picture.Height);
        renderer.DrawSolidRectangle(band.X, band.Y, band.Width, area.Y - band.Y, mask);
        renderer.DrawSolidRectangle(band.X, area.Y + area.Height, band.Width, band.Y + band.Height - area.Y - area.Height, mask);
        renderer.DrawSolidRectangle(band.X, area.Y, area.X - band.X, area.Height, mask);
        renderer.DrawSolidRectangle(area.X + area.Width, area.Y, band.X + band.Width - area.X - area.Width, area.Height, mask);
        renderer.DrawSprite(picture, area.X, area.Y, area.Width, area.Height);

        DrawDialogButton(renderer, controls, LoginDialogButton.Ok, _layout.Ok);
        DrawDialogButton(renderer, controls, LoginDialogButton.Cancel, _layout.Cancel);

        if (dialog == LoginAccountDialog.Clear && controls.DialogAccount is { } account)
        {
            // clearMC builds sDelID_0 + ' "' + name + '" ' + sDelID_1; neither sDelID string exists in 5517.
            if (!string.Equals(_messageAccount, account, StringComparison.Ordinal))
            {
                _messageAccount = account;
                _message = _font.LayoutUnicode($" \"{account}\" ");
            }

            if (_message is { HasMissingGlyphs: false } message)
            {
                _font.Draw(renderer, message, s_messageText, _layout.ClearMessage.X, _layout.ClearMessage.Y);
            }
        }
    }

    private void DrawDialogButton(OpenGLRenderer renderer, LoginAccountControls controls, LoginDialogButton button, LoginRect bounds)
    {
        LoginButtonState state = controls.DialogButtonState(button);
        if (state != LoginButtonState.Up)
        {
            OpenGLTexture2D texture = _dialogButtons[(int)button, state == LoginButtonState.Down ? 1 : 0];
            renderer.DrawSprite(texture, bounds.X, bounds.Y, bounds.Width, bounds.Height);
        }
    }

    private void UpdateRowLabels(IReadOnlyList<string> accounts)
    {
        for (int row = 0; row < _rowLabels.Length; row++)
        {
            string? name = row < accounts.Count ? accounts[row] : null;
            if (!string.Equals(_rowNames[row], name, StringComparison.Ordinal))
            {
                _rowNames[row] = name;
                _rowLabels[row] = name is null ? null : _font.LayoutUnicode(name);
            }
        }
    }

    private OpenGLTexture2D Create(OpenGLGraphicsDevice graphicsDevice, RgbaImage image) =>
        _ownership.Track(graphicsDevice.CreateTexture2D(image.Width, image.Height, image.Pixels.Span));

    private static NativeTextRenderOptions Style(SpriteColor color) =>
        new(NativeTextRenderStyle.Normal, color, color, 0, 0, NativeTextVertexColors.Solid(color));
}
