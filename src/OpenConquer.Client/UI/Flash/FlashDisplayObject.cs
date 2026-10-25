using OpenConquer.Content.Flash;

namespace OpenConquer.Client.UI.Flash;

/// <summary>
/// One instance on a Flash display list. The timeline places and moves it until a script writes one of its
/// transform properties; from then on, as in Flash Player, the timeline no longer changes its matrix or color.
/// </summary>
internal abstract class FlashDisplayObject
{
    private FlashMatrix _matrix;
    private FlashColorTransform _colorTransform;

    protected FlashDisplayObject(FlashMovie movie, FlashCharacter character, FlashPlacement placement, FlashClip? parent)
    {
        ArgumentNullException.ThrowIfNull(movie);
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(placement);

        Movie = movie;
        Character = character;
        Parent = parent;
        Depth = placement.Depth;
        Name = placement.Name;
        PlacedFrame = placement.PlacedFrame;
        _matrix = placement.Matrix;
        _colorTransform = placement.ColorTransform;
        ClipDepth = placement.ClipDepth;
        Ratio = placement.Ratio;
    }

    public FlashMovie Movie
    {
        get; protected set;
    }

    public FlashCharacter Character
    {
        get; protected set;
    }

    public FlashClip? Parent
    {
        get;
    }

    public int Depth
    {
        get; internal set;
    }

    public string? Name
    {
        get;
    }

    /// <summary>The timeline frame whose PlaceObject created this instance; identifies it across seeks.</summary>
    public int PlacedFrame
    {
        get;
    }

    /// <summary>Set when this object masks the depths above it, up to and including this depth.</summary>
    public int? ClipDepth
    {
        get; private set;
    }

    public int? Ratio
    {
        get; private set;
    }

    public bool Visible { get; set; } = true;

    /// <summary>True once a script has written a transform property (<c>_x</c>, <c>_alpha</c>, …).</summary>
    public bool ScriptOwnsTransform
    {
        get; private set;
    }

    /// <summary>True once <c>swapDepths</c> has moved it: the timeline no longer removes or moves it.</summary>
    public bool ScriptOwnsDepth
    {
        get; internal set;
    }

    public FlashMatrix Matrix => _matrix;

    public FlashColorTransform ColorTransform => _colorTransform;

    /// <summary>AS2 <c>_x</c>.</summary>
    public float X
    {
        get => _matrix.Tx;
        set => SetMatrix(_matrix with { Tx = value });
    }

    /// <summary>AS2 <c>_y</c>.</summary>
    public float Y
    {
        get => _matrix.Ty;
        set => SetMatrix(_matrix with { Ty = value });
    }

    /// <summary>AS2 <c>_alpha</c>, in percent. Flash keeps the multiplier in 1/256 steps.</summary>
    public float Alpha
    {
        get => _colorTransform.AlphaMultiplier * 100f / 256f;
        set
        {
            ScriptOwnsTransform = true;
            _colorTransform = _colorTransform with
            {
                AlphaMultiplier = (int)(value * 256f / 100f)
            };
        }
    }

    /// <summary>Applies the timeline's placement for the current frame, unless a script owns the transform.</summary>
    internal void ApplyPlacement(FlashPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);

        if (!ScriptOwnsTransform)
        {
            _matrix = placement.Matrix;
            _colorTransform = placement.ColorTransform;
        }

        ClipDepth = placement.ClipDepth;
        Ratio = placement.Ratio;
    }

    internal bool IsSameInstance(FlashPlacement placement) =>
        placement.CharacterId == Character.Id && placement.PlacedFrame == PlacedFrame && placement.Depth == Depth;

    private void SetMatrix(FlashMatrix matrix)
    {
        ScriptOwnsTransform = true;
        _matrix = matrix;
    }
}

/// <summary>A shape, static text or morph shape.</summary>
internal sealed class FlashShapeInstance(FlashMovie movie, FlashCharacter character, FlashPlacement placement, FlashClip parent)
    : FlashDisplayObject(movie, character, placement, parent);

/// <summary>A dynamic or input text field; scripts set its text.</summary>
internal sealed class FlashTextInstance : FlashDisplayObject
{
    public FlashTextInstance(FlashMovie movie, FlashEditTextCharacter character, FlashPlacement placement, FlashClip parent)
        : base(movie, character, placement, parent)
    {
        Definition = character;
        Text = character.InitialText;
    }

    public FlashEditTextCharacter Definition
    {
        get;
    }

    /// <summary>AS2 <c>text</c>.</summary>
    public string Text
    {
        get; set;
    }
}
