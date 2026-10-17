using System.Runtime.ExceptionServices;
using OpenConquer.Content.Startup;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Client.UI.Login;

/// <summary>Original retail 5517 server-tile rollover and selection sprites, owned by one OpenGL context.</summary>
internal sealed class LoginRetailSelectionPresentation : IDisposable
{
    private readonly OpenGLTexture2D?[] _atlases = new OpenGLTexture2D?[3];
    private readonly LoginVisualLayout _layout;
    private bool _disposed;

    public LoginRetailSelectionPresentation(OpenGLGraphicsDevice graphics, LoginVisualLayout layout)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        int count = 0;
        try
        {
            for (; count < _atlases.Length; count++)
            {
                // Status lights stay unallocated until an authenticated live-status source exists.
                if (count == (int)LoginRetailSelectionArtwork.Sequence.RealmStatus)
                    continue;
                LoginRetailSelectionArtwork.Sequence sequence = (LoginRetailSelectionArtwork.Sequence)count;
                var image = LoginRetailSelectionArtwork.Load(sequence);
                _atlases[count] = graphics.CreateTexture2D(image.Width, image.Height, image.Pixels.Span);
            }
        }
        catch
        {
            for (int index = count - 1; index >= 0; index--)
            {
                try
                {
                    _atlases[index]?.Dispose();
                }
                catch { /* Preserve the original initialization failure. */ }
            }
            throw;
        }
    }

    /// <summary>Sprite 179 frame-6 rollover (sprite 83) and frame-12 selection (sprite 178), above the son movie.</summary>
    public void Draw(OpenGLRenderer renderer, LoginServerSelection selection, LoginRetailSelectionMotion motion)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(motion);

        ReadOnlySpan<LoginStagePoint> holders = _layout.ServerHolders;
        int hoveredServer = motion.HoveredServerSlot;
        if ((uint)hoveredServer < (uint)holders.Length &&
            selection.ServerAtSlot(hoveredServer) is not null && hoveredServer != selection.SelectedServerSlot)
        {
            // Sprite 179 frame 6 nests sprite 83 at (-22.65, -9) within the server holder.
            DrawFrame(renderer, LoginRetailSelectionArtwork.Sequence.ServerHover, motion.ServerHoverFrame, holders[hoveredServer], -23, -9);
        }

        // The original client initially seeded code 3 before its optional status
        // refresh. A modern live status indicator requires separately authenticated
        // and fresh realm telemetry; do not present that seed as a current health claim.

        int selectedServer = selection.SelectedServerSlot;
        if ((uint)selectedServer < (uint)holders.Length)
        {
            // Sprite 179 frame 12 places sprite 178 at (-26, -29) at its own root depth 8.
            DrawFrame(renderer, LoginRetailSelectionArtwork.Sequence.ServerSelected, motion.ServerSelectedFrame, holders[selectedServer], -26, -29);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        ExceptionDispatchInfo? first = null;
        for (int i = _atlases.Length - 1; i >= 0; i--)
        {
            try
            {
                _atlases[i]?.Dispose();
            }
            catch (Exception exception) { first ??= ExceptionDispatchInfo.Capture(exception); }
        }
        first?.Throw();
    }

    private void DrawFrame(OpenGLRenderer renderer, LoginRetailSelectionArtwork.Sequence sequence, int frame, LoginStagePoint holder,
        int offsetX, int offsetY)
    {
        LoginRetailSelectionArtwork.Sheet sheet = LoginRetailSelectionArtwork.Describe(sequence);
        if ((uint)frame >= (uint)sheet.Count)
            throw new ArgumentOutOfRangeException(nameof(frame));
        SpriteSourceRectangle source = new((frame % sheet.Columns) * sheet.FrameWidth,
            (frame / sheet.Columns) * sheet.FrameHeight, sheet.FrameWidth, sheet.FrameHeight);
        LoginSpritePlacement target = _layout.Offset(holder, offsetX, offsetY, sheet.FrameWidth, sheet.FrameHeight);
        renderer.DrawSprite(_atlases[(int)sequence] ?? throw new InvalidOperationException("Retail selection atlas is missing."),
            source, target.X, target.Y, target.Width, target.Height);
    }
}
