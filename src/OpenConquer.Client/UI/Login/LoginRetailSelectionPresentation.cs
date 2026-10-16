using System.Runtime.ExceptionServices;
using OpenConquer.Content.Startup;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Client.UI.Login;

/// <summary>Original retail 5517 selection and rollover sprites, owned by one OpenGL context.</summary>
internal sealed class LoginRetailSelectionPresentation : IDisposable
{
    private readonly OpenGLTexture2D[] _atlases = new OpenGLTexture2D[4];
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
                var sequence = (LoginRetailSelectionArtwork.Sequence)count;
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
                    _atlases[index].Dispose();
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

        int hoveredRegion = motion.RegionBorderSlot;
        if ((uint)hoveredRegion < (uint)_layout.RegionCells.Length &&
            selection.RegionAtSlot(hoveredRegion) is not null && hoveredRegion != selection.SelectedRegionSlot)
        {
            LoginSpritePlacement rect = _layout.RegionCells[hoveredRegion];
            // Group1 root hover outer border, without the SWF's hard-coded MYTHS text.
            DrawFrame(renderer, LoginRetailSelectionArtwork.Sequence.RegionHoverBorder, motion.RegionBorderFrame,
                rect.X - Scale(2), rect.Y - Scale(4));
        }

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
                _atlases[i].Dispose();
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
        renderer.DrawSprite(_atlases[(int)sequence], source, x, y, Scale(sheet.FrameWidth), Scale(sheet.FrameHeight));
    }

    private int Scale(int value) => checked((int)(((long)value * _layout.Width + 512) / 1024));
}
