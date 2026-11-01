using System.Numerics;
using OpenConquer.Content.Flash;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.OpenGL.Text;
using OpenConquer.Rendering.Sprites;
using OpenConquer.Rendering.Text.Fonts;
using OpenConquer.Rendering.Text.Rendering;

namespace OpenConquer.Client.UI.Flash;

/// <summary>
/// Draws a <see cref="FlashStage"/> in display-list order. Rasters become premultiplied textures on first use;
/// masks become renderer mask levels; text fields are drawn with device fonts, as Flash does for fields without
/// embedded outlines.
/// </summary>
internal sealed class FlashStagePresentation : IDisposable
{
    private const int TextGutter = 2;
    private const int MaximumCachedLayouts = 512;

    private readonly OpenGLGraphicsDevice _graphicsDevice;
    private readonly IFlashRasterSource _rasters;
    private readonly Dictionary<(FlashMovie Movie, int Raster), OpenGLTexture2D> _textures = [];
    private readonly Dictionary<(string Font, int Size), OpenGLTextContext> _fonts = [];
    private readonly Dictionary<(OpenGLTextContext Context, string Text), OpenGLTextLayout> _layouts = [];
    private FlashStage? _drawing;
    private bool _disposed;

    public FlashStagePresentation(OpenGLGraphicsDevice graphicsDevice, IFlashRasterSource rasters)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(rasters);

        _graphicsDevice = graphicsDevice;
        _rasters = rasters;
    }

    public void Draw(OpenGLRenderer renderer, FlashStage stage)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(stage);

        _drawing = stage;
        try
        {
            DrawChildren(renderer, stage.Root, FlashTransforms.ToMatrix(stage.Root.Matrix), Color(stage.Root.ColorTransform));
        }
        finally
        {
            _drawing = null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (OpenGLTexture2D texture in _textures.Values)
        {
            texture.Dispose();
        }

        foreach (OpenGLTextContext font in _fonts.Values)
        {
            font.Dispose();
        }

        _textures.Clear();
        _fonts.Clear();
        _layouts.Clear();
    }

    private void DrawChildren(OpenGLRenderer renderer, FlashClip clip, Matrix3x2 world, SpriteColorTransform color)
    {
        Stack<int> masks = [];
        foreach (FlashDisplayObject child in clip.Children)
        {
            while (masks.Count > 0 && child.Depth > masks.Peek())
            {
                masks.Pop();
                renderer.EndMask();
            }

            Matrix3x2 childWorld = FlashTransforms.ToMatrix(child.Matrix) * world;
            if (child.ClipDepth is int clipDepth)
            {
                renderer.BeginMask();
                DrawCoverage(renderer, child, childWorld);
                renderer.BeginMaskedContent();
                masks.Push(clipDepth);
                continue;
            }

            if (!child.Visible)
            {
                continue;
            }

            SpriteColorTransform childColor = color.Concatenate(Color(child.ColorTransform));
            switch (child)
            {
                case FlashClip nested:
                    DrawChildren(renderer, nested, childWorld, childColor);
                    break;
                case FlashShapeInstance shape:
                    DrawRaster(renderer, shape.Movie, shape.Character, shape.Ratio, childWorld, childColor);
                    break;
                case FlashButtonInstance button:
                    DrawButton(renderer, button, childWorld, childColor);
                    break;
                case FlashTextInstance text:
                    DrawText(renderer, text, childWorld, childColor);
                    break;
            }
        }

        while (masks.Count > 0)
        {
            masks.Pop();
            renderer.EndMask();
        }
    }

    /// <summary>A mask's shape: everything it draws, ignoring color, visibility and nested masks.</summary>
    private void DrawCoverage(OpenGLRenderer renderer, FlashDisplayObject mask, Matrix3x2 world)
    {
        switch (mask)
        {
            case FlashShapeInstance shape:
                DrawRaster(renderer, shape.Movie, shape.Character, shape.Ratio, world, SpriteColorTransform.Identity);
                break;
            case FlashClip clip:
                foreach (FlashDisplayObject child in clip.Children)
                {
                    if (child.ClipDepth is null)
                    {
                        DrawCoverage(renderer, child, FlashTransforms.ToMatrix(child.Matrix) * world);
                    }
                }

                break;
        }
    }

    private void DrawButton(OpenGLRenderer renderer, FlashButtonInstance button, Matrix3x2 world, SpriteColorTransform color)
    {
        FlashButtonStates state = button.VisibleState;
        foreach (FlashButtonRecord record in button.Definition.Records)
        {
            if ((record.States & state) != 0)
            {
                DrawRaster(renderer, button.Movie, button.Movie.Character(record.CharacterId), null,
                    FlashTransforms.ToMatrix(record.Matrix) * world, color.Concatenate(Color(record.ColorTransform)));
            }
        }
    }

    private void DrawRaster(OpenGLRenderer renderer, FlashMovie movie, FlashCharacter character, int? ratio, Matrix3x2 world, SpriteColorTransform color)
    {
        if (color.AlphaMultiplier <= 0f && color.AlphaOffset <= 0f)
        {
            return;
        }

        foreach (FlashRasterPiece piece in FlashTransforms.PiecesOf(character, ratio))
        {
            Matrix3x2 transform = FlashTransforms.DevicePlacement(piece, world);
            FlashRaster info = movie.Rasters[piece.Raster];
            renderer.DrawTransformedSprite(Texture(movie, piece.Raster), new SpriteSourceRectangle(0, 0, info.Width, info.Height), transform, color);
        }
    }

    internal int LocateCaret(FlashTextInstance text, Vector2 pointer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(text);

        FlashEditTextCharacter field = text.Definition;
        OpenGLTextContext font = Font(field.Font, (int)MathF.Round(field.FontHeight));
        string value = field.Password ? new string('*', text.Text.Length) : text.Text;
        float width = field.Bounds.Right - field.Bounds.Left - 2 * TextGutter - field.LeftMargin - field.RightMargin;
        Matrix3x2 world = FlashTransforms.World(text);
        Vector2 origin = Vector2.Transform(new Vector2(field.Bounds.Left + TextGutter + field.LeftMargin,
            field.Bounds.Top + TextGutter), world);
        OpenGLTextLayout layout = Layout(font, value);
        float offset = field.Alignment switch
        {
            FlashTextAlignment.Center => (width - layout.WidthPixels) / 2f,
            FlashTextAlignment.Right => width - layout.WidthPixels,
            _ => 0f,
        };

        float x = pointer.X - origin.X - offset;
        int best = 0;
        float distance = MathF.Abs(x);
        for (int index = 1; index <= value.Length; index++)
        {
            float next = MathF.Abs(x - Layout(font, value[..index]).WidthPixels);
            if (next < distance)
            {
                best = index;
                distance = next;
            }
        }

        return best;
    }

    private void DrawText(OpenGLRenderer renderer, FlashTextInstance text, Matrix3x2 world, SpriteColorTransform color)
    {
        FlashEditTextCharacter field = text.Definition;
        string value = field.Password ? new string('*', text.Text.Length) : text.Text;
        // Retail never shows the player's caret: the role.dat captures with a focused field (vm-runtime 18, 20, 21, 23, 34)
        // have none, and the login host draws its own caret from CMD_TEXTSELECT, which role.dat does not send.
        bool focused = ReferenceEquals(_drawing?.FocusedText, text);
        if (value.Length == 0 || color.AlphaMultiplier <= 0f)
        {
            return;
        }

        OpenGLTextContext font = Font(field.Font, (int)MathF.Round(field.FontHeight));
        float width = field.Bounds.Right - field.Bounds.Left - 2 * TextGutter - field.LeftMargin - field.RightMargin;
        Vector2 origin = Vector2.Transform(new Vector2(field.Bounds.Left + TextGutter + field.LeftMargin, field.Bounds.Top + TextGutter), world);
        SpriteColor tint = new(Channel(field.Red, color.RedMultiplier, color.RedOffset), Channel(field.Green, color.GreenMultiplier, color.GreenOffset),
            Channel(field.Blue, color.BlueMultiplier, color.BlueOffset), Channel(field.Alpha, color.AlphaMultiplier, color.AlphaOffset));
        NativeTextRenderOptions style = new(NativeTextRenderStyle.Normal, tint, tint, 0, 0, NativeTextVertexColors.Solid(tint));

        // Flash clips a field's text to its bounds and draws only the lines that fit whole: jobInfo's fourth line stays
        // hidden on the class stage (vm-runtime 33).
        DeviceRectangle? bounds = FieldBounds(field, world);
        if (bounds is { IsEmpty: true })
        {
            return;
        }

        using OpenGLRenderer.ClipScope clip = bounds is { } area ? renderer.Clip(area.Left, area.Top, area.Width, area.Height) : default;
        int lineAdvance = Math.Max(1, (int)MathF.Round(field.FontHeight + field.Leading)) + TextGutter;
        int lineY = DevicePixel(origin.Y);
        foreach (string line in Lines(font, value, field.Multiline && field.WordWrap ? width : float.PositiveInfinity, field.Multiline))
        {
            if (bounds is { } limit && lineY + lineAdvance > limit.Bottom)
            {
                break;
            }

            OpenGLTextLayout layout = Layout(font, line);
            float offset = field.Alignment switch
            {
                FlashTextAlignment.Center => (width - layout.WidthPixels) / 2f,
                FlashTextAlignment.Right => width - layout.WidthPixels,
                _ => 0f,
            };

            if (focused && !field.Multiline && text.HasSelection && !field.NoSelect)
            {
                int start = Layout(font, value[..text.SelectionStart]).WidthPixels;
                int end = Layout(font, value[..text.SelectionEnd]).WidthPixels;
                renderer.DrawSolidRectangle(DevicePixel(origin.X + offset) + start, lineY,
                    Math.Max(1, end - start), Math.Max(1, (int)MathF.Round(field.FontHeight)),
                    new SpriteColor(48, 111, 207, 160));
            }

            if (line.Length != 0)
            {
                font.Draw(renderer, layout, style, DevicePixel(origin.X + offset), lineY);
            }

            lineY += lineAdvance;
        }
    }

    /// <summary>The field's bounds in device pixels, clamped to the stage; null when the field is rotated or skewed.</summary>
    private DeviceRectangle? FieldBounds(FlashEditTextCharacter field, Matrix3x2 world)
    {
        if (world.M12 != 0f || world.M21 != 0f)
        {
            return null;
        }

        Vector2 a = Vector2.Transform(new Vector2(field.Bounds.Left, field.Bounds.Top), world);
        Vector2 b = Vector2.Transform(new Vector2(field.Bounds.Right, field.Bounds.Bottom), world);
        FlashMovie stageMovie = _drawing!.Root.Movie;
        return new DeviceRectangle(
            Math.Clamp(DevicePixel(MathF.Min(a.X, b.X)), 0, stageMovie.Width),
            Math.Clamp(DevicePixel(MathF.Min(a.Y, b.Y)), 0, stageMovie.Height),
            Math.Clamp(DevicePixel(MathF.Max(a.X, b.X)), 0, stageMovie.Width),
            Math.Clamp(DevicePixel(MathF.Max(a.Y, b.Y)), 0, stageMovie.Height));
    }

    /// <summary>Flash rounds a device-text position half up: sprite 304's userNameTxt at x 790.5 draws from 791.</summary>
    private static int DevicePixel(float value) => (int)MathF.Floor(value + 0.5f);

    private IEnumerable<string> Lines(OpenGLTextContext font, string value, float width, bool multiline)
    {
        string[] paragraphs = value.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\r', '\n');
        if (!multiline)
        {
            yield return string.Join(' ', paragraphs);
            yield break;
        }

        foreach (string paragraph in paragraphs)
        {
            if (float.IsPositiveInfinity(width))
            {
                yield return paragraph;
                continue;
            }

            string line = string.Empty;
            foreach (string word in paragraph.Split(' '))
            {
                string candidate = line.Length == 0 ? word : line + " " + word;
                if (line.Length != 0 && Layout(font, candidate).WidthPixels > width)
                {
                    yield return line;
                    line = word;
                }
                else
                {
                    line = candidate;
                }
            }

            yield return line;
        }
    }

    private OpenGLTexture2D Texture(FlashMovie movie, int raster)
    {
        if (_textures.TryGetValue((movie, raster), out OpenGLTexture2D? texture))
        {
            return texture;
        }

        FlashRaster info = movie.Rasters[raster];
        byte[] premultiplied = _rasters.Pixels(movie, raster).ToArray();
        for (int offset = 0; offset < premultiplied.Length; offset += 4)
        {
            int alpha = premultiplied[offset + 3];
            if (alpha != byte.MaxValue)
            {
                premultiplied[offset] = (byte)((premultiplied[offset] * alpha + 127) / 255);
                premultiplied[offset + 1] = (byte)((premultiplied[offset + 1] * alpha + 127) / 255);
                premultiplied[offset + 2] = (byte)((premultiplied[offset + 2] * alpha + 127) / 255);
            }
        }

        texture = _graphicsDevice.CreateTexture2D(info.Width, info.Height, premultiplied);
        _textures.Add((movie, raster), texture);
        return texture;
    }

    private OpenGLTextContext Font(string name, int size)
    {
        // Flash names the Chinese device font by its family name; SimSun is the file the client ships with.
        string token = name == "宋体" ? "$simsun.ttc" : name;
        if (!_fonts.TryGetValue((token, size), out OpenGLTextContext? font))
        {
            font = new OpenGLTextContext(_graphicsDevice, token, Math.Max(1, size), effectiveCodePage: 0, antialiasEnabled: true, bold: false,
                GlyphHinting.Gdi);
            _fonts.Add((token, size), font);
        }

        return font;
    }

    private OpenGLTextLayout Layout(OpenGLTextContext font, string text)
    {
        if (!_layouts.TryGetValue((font, text), out OpenGLTextLayout? layout))
        {
            if (_layouts.Count == MaximumCachedLayouts)
            {
                _layouts.Clear();
            }

            layout = font.LayoutUnicode(text);
            _layouts.Add((font, text), layout);
        }

        return layout;
    }

    private static SpriteColorTransform Color(FlashColorTransform transform) =>
        SpriteColorTransform.FromSwf(transform.RedMultiplier, transform.GreenMultiplier, transform.BlueMultiplier, transform.AlphaMultiplier,
            transform.RedAdd, transform.GreenAdd, transform.BlueAdd, transform.AlphaAdd);

    private static byte Channel(byte value, float multiplier, float offset) =>
        (byte)Math.Clamp((int)MathF.Round((value / 255f * multiplier + offset) * 255f), 0, 255);


    private readonly record struct DeviceRectangle(int Left, int Top, int Right, int Bottom)
    {
        public int Width => Right - Left;

        public int Height => Bottom - Top;

        public bool IsEmpty => Width <= 0 || Height <= 0;
    }
}
