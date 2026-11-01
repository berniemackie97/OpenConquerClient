using System.Numerics;
using OpenConquer.Content.Flash;
using OpenConquer.Content.Images;

namespace OpenConquer.Client.UI.Flash;

/// <summary>Decoded straight-RGBA pixels of baked movie rasters, shared by hit testing and drawing.</summary>
internal interface IFlashRasterSource
{
    ReadOnlyMemory<byte> Pixels(FlashMovie movie, int index);
}

/// <summary>Loads each raster once and keeps it.</summary>
internal sealed class FlashRasterCache : IFlashRasterSource
{
    private readonly Dictionary<(FlashMovie Movie, int Index), RgbaImage> _rasters = [];

    public ReadOnlyMemory<byte> Pixels(FlashMovie movie, int index)
    {
        ArgumentNullException.ThrowIfNull(movie);

        if (!_rasters.TryGetValue((movie, index), out RgbaImage? image))
        {
            image = movie.LoadRaster(index);
            _rasters.Add((movie, index), image);
        }

        return image.Pixels;
    }
}

internal static class FlashTransforms
{
    private const float UnscaledTolerance = 0.001f;

    public static Matrix3x2 ToMatrix(FlashMatrix matrix) => new(matrix.A, matrix.B, matrix.C, matrix.D, matrix.Tx, matrix.Ty);

    /// <summary>Character space of <paramref name="displayObject"/> to stage pixels.</summary>
    public static Matrix3x2 World(FlashDisplayObject displayObject)
    {
        ArgumentNullException.ThrowIfNull(displayObject);

        Matrix3x2 world = ToMatrix(displayObject.Matrix);
        for (FlashClip? parent = displayObject.Parent; parent is not null; parent = parent.Parent)
        {
            world *= ToMatrix(parent.Matrix);
        }

        return world;
    }

    /// <summary>
    /// The device transform of a raster piece. Flash draws an unscaled, unrotated bitmap on whole pixels; scales within
    /// 1/1000 of one (sprite 304 places the name box at a y scale of 1.000412) count as unscaled, as retail draws
    /// that box crisp. See <see cref="FlashPixelSnap"/> for the rounding.
    /// </summary>
    public static Matrix3x2 DevicePlacement(FlashRasterPiece piece, Matrix3x2 world)
    {
        Matrix3x2 transform = Matrix3x2.CreateTranslation(piece.OriginX, piece.OriginY) * world;
        if (MathF.Abs(transform.M11 - 1f) < UnscaledTolerance && MathF.Abs(transform.M22 - 1f) < UnscaledTolerance &&
            transform.M12 == 0f && transform.M21 == 0f)
        {
            float bias = piece.Snap == FlashPixelSnap.PixelCenter ? 0.5f : 0f;
            transform = Matrix3x2.CreateTranslation(MathF.Ceiling(transform.M31 - bias), MathF.Ceiling(transform.M32 - bias));
        }

        return transform;
    }

    /// <summary>The raster pieces a shape instance shows now, in drawing order.</summary>
    public static IReadOnlyList<FlashRasterPiece> PiecesOf(FlashCharacter character, int? ratio) => character switch
    {
        FlashRasterCharacter shape => shape.Pieces,
        FlashMorphCharacter morph => [new FlashRasterPiece(morph.RasterByRatio[ratio ?? 0], morph.OriginX, morph.OriginY, FlashPixelSnap.PixelCenter)],
        FlashVideoCharacter video => [new FlashRasterPiece(video.RasterAt(ratio ?? 0) ??
            throw new InvalidOperationException($"Video {video.Id} has no frame at or before ratio {ratio ?? 0}."), 0f, 0f, FlashPixelSnap.PixelCorner)],
        _ => throw new ArgumentException($"Character {character.Id} is not drawn from a raster.", nameof(character)),
    };
}

/// <summary>
/// Finds the pointer target as Flash does: the topmost enabled button, or clip with pointer handlers, whose shape
/// (or <c>hitArea</c>) is under the point and inside every mask over it. Shapes hit where their raster is not
/// transparent; invisible objects do not hit, but transparent ones do.
/// </summary>
internal sealed class FlashHitTester(IFlashRasterSource rasters)
{
    private readonly Dictionary<(FlashMovie Movie, int Raster), bool> _rasterHasVisiblePixels = [];

    public IFlashPointerTarget? Target(FlashClip root, Vector2 point)
    {
        ArgumentNullException.ThrowIfNull(root);

        return FindIn(root, point);
    }

    /// <summary>The topmost visible input field under the point, inside every mask over it.</summary>
    public FlashTextInstance? InputTextAt(FlashClip root, Vector2 point)
    {
        ArgumentNullException.ThrowIfNull(root);

        IReadOnlyList<FlashDisplayObject> children = root.Children;
        for (int index = children.Count - 1; index >= 0; index--)
        {
            FlashDisplayObject child = children[index];
            if (!child.Visible || child.ClipDepth is not null || !InsideMasks(children, index, point))
            {
                continue;
            }

            if (child is FlashTextInstance { IsInput: true } text && Contains(text, point, ignoreVisibility: false))
            {
                return text;
            }

            if (child is FlashClip nested && InputTextAt(nested, point) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private IFlashPointerTarget? FindIn(FlashClip clip, Vector2 point)
    {
        IReadOnlyList<FlashDisplayObject> children = clip.Children;
        for (int index = children.Count - 1; index >= 0; index--)
        {
            FlashDisplayObject child = children[index];
            if (!child.Visible || child.ClipDepth is not null || !InsideMasks(children, index, point))
            {
                continue;
            }

            switch (child)
            {
                case FlashButtonInstance button when button.Enabled && ContainsButton(button, point):
                    return button;
                case FlashClip target when target.HandlesPointer:
                    if (!target.Enabled)
                    {
                        break;
                    }

                    if (target.HitArea is { } hitArea ? Contains(hitArea, point, ignoreVisibility: true, hitGeometry: true) : Contains(target, point, ignoreVisibility: false))
                    {
                        return target;
                    }

                    break;
                case FlashClip nested:
                    if (FindIn(nested, point) is { } found)
                    {
                        return found;
                    }

                    break;
            }
        }

        return null;
    }

    private bool InsideMasks(IReadOnlyList<FlashDisplayObject> siblings, int index, Vector2 point)
    {
        int depth = siblings[index].Depth;
        for (int below = index - 1; below >= 0; below--)
        {
            if (siblings[below].ClipDepth is int clipDepth && clipDepth >= depth &&
                !MaskContains(siblings[below], point))
            {
                return false;
            }
        }

        return true;
    }

    // Clip masks use the union of their non-mask descendants as coverage, regardless of
    // nested masks or visibility. Match DrawCoverage rather than interactive hit testing.
    private bool MaskContains(FlashDisplayObject mask, Vector2 point)
    {
        switch (mask)
        {
            case FlashShapeInstance shape:
                return RasterContains(shape.Movie, shape.Character, shape.Ratio, FlashTransforms.World(shape), point);
            case FlashClip clip:
                foreach (FlashDisplayObject child in clip.Children)
                {
                    if (child.ClipDepth is null && MaskContains(child, point))
                    {
                        return true;
                    }
                }

                return false;
            default:
                return false;
        }
    }

    private bool Contains(FlashDisplayObject displayObject, Vector2 point, bool ignoreVisibility, bool hitGeometry = false)
    {
        if (!ignoreVisibility && !displayObject.Visible)
        {
            return false;
        }

        switch (displayObject)
        {
            case FlashShapeInstance shape:
                return RasterContains(shape.Movie, shape.Character, shape.Ratio, FlashTransforms.World(shape), point, hitGeometry);
            case FlashTextInstance text:
                return Local(FlashTransforms.World(text), point) is { } local &&
                       local.X >= text.Definition.Bounds.Left && local.X < text.Definition.Bounds.Right &&
                       local.Y >= text.Definition.Bounds.Top && local.Y < text.Definition.Bounds.Bottom;
            case FlashButtonInstance button:
                return ContainsButton(button, point);
            case FlashClip clip:
                IReadOnlyList<FlashDisplayObject> children = clip.Children;
                for (int index = 0; index < children.Count; index++)
                {
                    if (children[index].ClipDepth is null && InsideMasks(children, index, point) && Contains(children[index], point, ignoreVisibility, hitGeometry))
                    {
                        return true;
                    }
                }

                return false;
            default:
                return false;
        }
    }

    private bool ContainsButton(FlashButtonInstance button, Vector2 point)
    {
        Matrix3x2 world = FlashTransforms.World(button);
        foreach (FlashButtonRecord record in button.Definition.Records)
        {
            if ((record.States & FlashButtonStates.Hit) != 0 &&
                RasterContains(button.Movie, button.Movie.Character(record.CharacterId), null, FlashTransforms.ToMatrix(record.Matrix) * world, point))
            {
                return true;
            }
        }

        return false;
    }

    private bool RasterContains(FlashMovie movie, FlashCharacter character, int? ratio, Matrix3x2 world, Vector2 point, bool hitGeometry = false)
    {
        if (Local(world, point) is not { } local)
        {
            return false;
        }

        foreach (FlashRasterPiece piece in FlashTransforms.PiecesOf(character, ratio))
        {
            if (PieceContains(movie, piece, local, hitGeometry))
            {
                return true;
            }
        }

        return false;
    }

    private bool PieceContains(FlashMovie movie, FlashRasterPiece piece, Vector2 local, bool hitGeometry)
    {
        int raster = piece.Raster;
        int x = (int)MathF.Floor(local.X - piece.OriginX);
        int y = (int)MathF.Floor(local.Y - piece.OriginY);
        FlashRaster info = movie.Rasters[raster];
        if (x < 0 || y < 0 || x >= info.Width || y >= info.Height)
        {
            return false;
        }

        ReadOnlySpan<byte> pixels = rasters.Pixels(movie, raster).Span;
        if (hitGeometry)
        {
            if (!_rasterHasVisiblePixels.TryGetValue((movie, raster), out bool hasVisiblePixels))
            {
                hasVisiblePixels = false;
                for (int offset = 3; offset < pixels.Length; offset += 4)
                {
                    if (pixels[offset] == 0)
                    {
                        continue;
                    }

                    hasVisiblePixels = true;
                    break;
                }

                _rasterHasVisiblePixels.Add((movie, raster), hasVisiblePixels);
            }

            if (!hasVisiblePixels)
            {
                return true;
            }
        }

        return pixels[(y * info.Width + x) * 4 + 3] != 0;
    }

    private static Vector2? Local(Matrix3x2 world, Vector2 point) =>
        Matrix3x2.Invert(world, out Matrix3x2 inverse) ? Vector2.Transform(point, inverse) : null;
}
