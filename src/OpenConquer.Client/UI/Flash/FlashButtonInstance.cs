using OpenConquer.Content.Flash;

namespace OpenConquer.Client.UI.Flash;

/// <summary>Something the pointer can target: a button, or a clip with pointer handlers.</summary>
internal interface IFlashPointerTarget
{
    void RollOver();

    void RollOut();

    void Press();

    void Release();

    void ReleaseOutside();
}

/// <summary>
/// A SWF button. It shows its up, over or down records from the pointer state and is hit-tested by its hit
/// records; scripts attach the same handlers as on clips.
/// </summary>
internal sealed class FlashButtonInstance : FlashDisplayObject, IFlashPointerTarget
{
    private bool _hovered;
    private bool _pressed;

    public FlashButtonInstance(FlashMovie movie, FlashButtonCharacter character, FlashPlacement placement, FlashClip parent)
        : base(movie, character, placement, parent)
    {
        foreach (FlashButtonRecord record in character.Records)
        {
            if (movie.Character(record.CharacterId) is not (FlashRasterCharacter or FlashMorphCharacter))
            {
                throw new NotSupportedException($"Button {character.Id} shows character {record.CharacterId}, which is not a shape.");
            }
        }

        Definition = character;
    }

    public FlashButtonCharacter Definition
    {
        get;
    }

    public Action<FlashButtonInstance>? OnRollOver
    {
        get; set;
    }

    public Action<FlashButtonInstance>? OnRollOut
    {
        get; set;
    }

    public Action<FlashButtonInstance>? OnPress
    {
        get; set;
    }

    public Action<FlashButtonInstance>? OnRelease
    {
        get; set;
    }

    public Action<FlashButtonInstance>? OnReleaseOutside
    {
        get; set;
    }

    /// <summary>AS2 <c>enabled</c>: a disabled button shows its up state and ignores the pointer.</summary>
    public bool Enabled { get; set; } = true;

    public FlashButtonStates VisibleState => !Enabled || !_hovered ? FlashButtonStates.Up : _pressed ? FlashButtonStates.Down : FlashButtonStates.Over;

    void IFlashPointerTarget.RollOver()
    {
        _hovered = true;
        OnRollOver?.Invoke(this);
    }

    void IFlashPointerTarget.RollOut()
    {
        _hovered = false;
        _pressed = false;
        OnRollOut?.Invoke(this);
    }

    void IFlashPointerTarget.Press()
    {
        _pressed = true;
        OnPress?.Invoke(this);
    }

    void IFlashPointerTarget.Release()
    {
        _pressed = false;
        OnRelease?.Invoke(this);
    }

    void IFlashPointerTarget.ReleaseOutside()
    {
        _pressed = false;
        _hovered = false;
        OnReleaseOutside?.Invoke(this);
    }
}
