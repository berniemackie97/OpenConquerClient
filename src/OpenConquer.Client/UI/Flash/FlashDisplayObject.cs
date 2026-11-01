using System.Globalization;
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
        PlacedCharacterId = placement.CharacterId;
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

    /// <summary>
    /// The character its PlaceObject named. It identifies the instance across seeks even after <c>loadMovie</c>
    /// has replaced the clip's content.
    /// </summary>
    public int PlacedCharacterId
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
        placement.CharacterId == PlacedCharacterId && placement.PlacedFrame == PlacedFrame && placement.Depth == Depth;

    private void SetMatrix(FlashMatrix matrix)
    {
        ScriptOwnsTransform = true;
        _matrix = matrix;
    }
}

/// <summary>A shape, static text or morph shape.</summary>
internal sealed class FlashShapeInstance(FlashMovie movie, FlashCharacter character, FlashPlacement placement, FlashClip parent)
    : FlashDisplayObject(movie, character, placement, parent);

/// <summary>
/// A dynamic or input text field. Scripts set its text; an input field (not read-only) also takes typing at a
/// caret while it has the stage's focus. The anchor and caret delimit the current selection.
/// </summary>
internal sealed class FlashTextInstance : FlashDisplayObject
{
    private string _text;
    private int _anchor;

    public FlashTextInstance(FlashMovie movie, FlashEditTextCharacter character, FlashPlacement placement, FlashClip parent)
        : base(movie, character, placement, parent)
    {
        Definition = character;
        _text = character.InitialText;
        if (Binding is { } variable && !parent.TryGet(variable, out _))
        {
            parent.Set(variable, _text);
        }
    }

    public FlashEditTextCharacter Definition
    {
        get;
    }

    public bool IsInput => !Definition.ReadOnly;

    /// <summary>AS2 <c>text</c>. Setting it from a script does not raise <see cref="OnChanged"/>.</summary>
    public string Text
    {
        get => Value;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            Value = value;
            Caret = Math.Min(Caret, value.Length);
            _anchor = Math.Min(_anchor, value.Length);
        }
    }

    /// <summary>
    /// The parent-timeline variable the field is bound to (<c>DefineEditText</c> <c>VariableName</c>). Flash reads the
    /// field's text from that variable and writes edits back to it, so a field the timeline replaces keeps its text.
    /// </summary>
    private string? Binding => Definition.Variable.Length == 0 ? null : Definition.Variable;

    private string Value
    {
        get => Binding is { } variable && Parent!.TryGet(variable, out object? bound) && bound is not null
            ? Convert.ToString(bound, CultureInfo.InvariantCulture) ?? string.Empty
            : _text;
        set
        {
            _text = value;
            if (Binding is { } variable)
            {
                Parent!.Set(variable, value);
            }
        }
    }

    /// <summary>Insertion point, in UTF-16 units.</summary>
    public int Caret
    {
        get; private set;
    }

    public int SelectionStart => Math.Min(_anchor, Caret);

    public int SelectionEnd => Math.Max(_anchor, Caret);

    public bool HasSelection => _anchor != Caret;

    public string SelectedText => Definition.Password || Definition.NoSelect ? string.Empty : Value[SelectionStart..SelectionEnd];

    internal Func<string, bool>? AcceptInsertion
    {
        get; set;
    }

    /// <summary>AS2 <c>onChanged</c>: raised after each edit the user makes.</summary>
    public Action<FlashTextInstance>? OnChanged
    {
        get; set;
    }

    /// <summary>AS2 <c>Selection.setSelection(begin, end)</c>, clamped to the field's text.</summary>
    public void SetSelection(int begin, int end)
    {
        _anchor = Math.Clamp(begin, 0, Value.Length);
        Caret = Math.Clamp(end, 0, Value.Length);
    }

    internal void Insert(ReadOnlySpan<char> characters)
    {
        if (!IsInput || characters.IsEmpty)
        {
            return;
        }

        foreach (char character in characters)
        {
            if (char.IsControl(character))
            {
                return;
            }
        }

        int start = SelectionStart;
        int end = SelectionEnd;
        int count = characters.Length;
        if (Definition.MaxLength > 0)
        {
            count = Math.Min(count, Definition.MaxLength - (Value.Length - (end - start)));
        }

        if (count <= 0 || AcceptInsertion is not null && count != characters.Length)
        {
            return;
        }

        string candidate = string.Concat(Value.AsSpan(0, start), characters[..count], Value.AsSpan(end));
        if (AcceptInsertion is not null && !AcceptInsertion(candidate))
        {
            return;
        }

        Value = candidate;
        Caret = _anchor = start + count;
        OnChanged?.Invoke(this);
    }

    internal void Backspace()
    {
        if (DeleteSelection())
        {
            return;
        }

        if (IsInput && Caret > 0)
        {
            Value = Value.Remove(Caret - 1, 1);
            Caret--;
            _anchor = Caret;
            OnChanged?.Invoke(this);
        }
    }

    internal void Delete()
    {
        if (DeleteSelection())
        {
            return;
        }

        if (IsInput && Caret < Value.Length)
        {
            Value = Value.Remove(Caret, 1);
            OnChanged?.Invoke(this);
        }
    }

    internal bool DeleteSelection()
    {
        if (!IsInput || !HasSelection || Definition.NoSelect)
        {
            return false;
        }

        int start = SelectionStart;
        Value = Value.Remove(start, SelectionEnd - start);
        Caret = _anchor = start;
        OnChanged?.Invoke(this);
        return true;
    }

    internal void MoveCaret(int position, bool extendSelection = false)
    {
        Caret = Math.Clamp(position, 0, Value.Length);
        if (!extendSelection || Definition.NoSelect)
        {
            _anchor = Caret;
        }
    }
}
