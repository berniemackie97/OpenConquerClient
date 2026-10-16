using System.Runtime.ExceptionServices;
using OpenConquer.Content.Startup;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Client.UI.Login;

/// <summary>Original retail 5517 selection and rollover sprites, owned by one OpenGL context.</summary>
internal sealed class LoginRetailSelectionPresentation : IDisposable
{
    private readonly OpenGLTexture2D?[] _atlases = new OpenGLTexture2D?[5];
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
                // Group1 hover is not a generic region skin. Do not allocate or draw it
                // for unrelated signed-directory regions.
                if (count == (int)LoginRetailSelectionArtwork.Sequence.RegionHoverBorder)
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

    public void Draw(OpenGLRenderer renderer, LoginServerSelection selection, LoginRetailSelectionMotion motion)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(motion);

        // Region movies have group-specific rollover artwork. The Group1 export is
        // not a valid fallback for arbitrary signed catalog regions, and cannot be
        // displayed as a generic hover state.

        int selectedRegion = selection.SelectedRegionSlot;
        if ((uint)selectedRegion < (uint)_layout.RegionCells.Length)
        {
            LoginSpritePlacement rect = LoginSelectionArtworkLayout.SelectedRegion(_layout, selectedRegion);
            DrawFrame(renderer, LoginRetailSelectionArtwork.Sequence.RegionFlame, motion.RegionFlameFrame, rect.X, rect.Y);
        }

        int hoveredServer = motion.HoveredServerSlot;
        if ((uint)hoveredServer < (uint)_layout.ServerCells.Length &&
            selection.ServerAtSlot(hoveredServer) is not null && hoveredServer != selection.SelectedServerSlot)
        {
            LoginSpritePlacement rect = _layout.ServerCells[hoveredServer];
            // Sprite 179 frame 6 nests sprite 83 at (-22.65,-9) within the server holder.
            DrawFrame(renderer, LoginRetailSelectionArtwork.Sequence.ServerHover, motion.ServerHoverFrame,
                rect.X - Scale(23), rect.Y - Scale(9));
        }

        // DefineSprite 179 depth 6: status-light 34 at (9.3, 62.25) relative to
        // the tile origin. This is the original startup status code 3 (green).
        // It is a visual seed, not an authenticated live occupancy signal.
        for (int slot = 0; slot < _layout.ServerCells.Length; slot++)
        {
            if (selection.ServerAtSlot(slot) is not null)
            {
                LoginSpritePlacement rect = _layout.ServerCells[slot];
                DrawFrame(renderer, LoginRetailSelectionArtwork.Sequence.RealmStatus, 2,
                    rect.X + Scale(8), rect.Y + Scale(61));
            }
        }

        int selectedServer = selection.SelectedServerSlot;
        if ((uint)selectedServer < (uint)_layout.ServerCells.Length)
        {
            LoginSpritePlacement rect = _layout.ServerCells[selectedServer];
            // Sprite 179 frame 12 places sprite 178 at (-26,-29) at its own root depth 8.
            DrawFrame(renderer, LoginRetailSelectionArtwork.Sequence.ServerSelected, motion.ServerSelectedFrame,
                rect.X - Scale(26), rect.Y - Scale(29));
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

    private void DrawFrame(OpenGLRenderer renderer, LoginRetailSelectionArtwork.Sequence sequence, int frame, int x, int y)
    {
        LoginRetailSelectionArtwork.Sheet sheet = LoginRetailSelectionArtwork.Describe(sequence);
        if ((uint)frame >= (uint)sheet.Count)
            throw new ArgumentOutOfRangeException(nameof(frame));
        SpriteSourceRectangle source = new((frame % sheet.Columns) * sheet.FrameWidth,
            (frame / sheet.Columns) * sheet.FrameHeight, sheet.FrameWidth, sheet.FrameHeight);
        renderer.DrawSprite(_atlases[(int)sequence] ?? throw new InvalidOperationException("Retail selection atlas is missing."),
            source, x, y, Scale(sheet.FrameWidth), Scale(sheet.FrameHeight));
    }

    private int Scale(int value) => checked((int)(((long)value * _layout.Width + 512) / 1024));
}
