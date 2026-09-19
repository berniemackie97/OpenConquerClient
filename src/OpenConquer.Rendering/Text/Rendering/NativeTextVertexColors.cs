namespace OpenConquer.Rendering.Text.Rendering;

/// <summary>
/// Defines the four native-ordered vertex colors applied to one text glyph quad.
/// </summary>
internal readonly record struct NativeTextVertexColors(SpriteColor TopLeft, SpriteColor BottomLeft, SpriteColor TopRight, SpriteColor BottomRight)
{
    public static NativeTextVertexColors Solid(SpriteColor color)
    {
        return new NativeTextVertexColors(color, color, color, color);
    }
}
