namespace OpenConquer.Content.Flash;

/// <summary>A display character of a baked movie.</summary>
public abstract record FlashCharacter(int Id);

/// <summary>
/// A shape or static text, drawn as one raster whose pixel (0,0) is the character-space point (OriginX, OriginY).
/// </summary>
public sealed record FlashRasterCharacter(int Id, int Raster, float OriginX, float OriginY, bool IsStaticText) : FlashCharacter(Id);

/// <summary>A morph shape, baked as one raster per ratio its timelines use.</summary>
public sealed record FlashMorphCharacter(int Id, float OriginX, float OriginY, IReadOnlyDictionary<int, int> RasterByRatio) : FlashCharacter(Id);

/// <summary>A movie clip with its own timeline.</summary>
public sealed record FlashSpriteCharacter(int Id, FlashTimeline Timeline) : FlashCharacter(Id);

/// <summary>A dynamic or input text field. Text is drawn at run time with a device font.</summary>
public sealed record FlashEditTextCharacter(int Id, FlashBounds Bounds, string Font, float FontHeight,
    byte Red, byte Green, byte Blue, byte Alpha, FlashTextAlignment Alignment, float LeftMargin, float RightMargin,
    float Indent, float Leading, int MaxLength, string Variable, string InitialText, bool ReadOnly, bool Password,
    bool Multiline, bool WordWrap, bool Html, bool NoSelect, bool Border, bool AutoSize) : FlashCharacter(Id);

public enum FlashTextAlignment
{
    Left = 0,
    Right = 1,
    Center = 2,
    Justify = 3,
}

/// <summary>A button: each record shows a character in some of its up, over, down and hit states.</summary>
public sealed record FlashButtonCharacter(int Id, IReadOnlyList<FlashButtonRecord> Records) : FlashCharacter(Id);

[Flags]
public enum FlashButtonStates
{
    None = 0,
    Up = 1,
    Over = 2,
    Down = 4,
    Hit = 8,
}

public sealed record FlashButtonRecord(int CharacterId, int Depth, FlashButtonStates States, FlashMatrix Matrix, FlashColorTransform ColorTransform);
