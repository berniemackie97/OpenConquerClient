using System.Globalization;
using OpenConquer.Content.Flash;

namespace OpenConquer.Client.UI.Flash;

/// <summary>
/// A movie clip: a timeline instance with its own children. Seeking rebuilds the display list from the target
/// frame's snapshot, keeping every child whose depth still holds the same character placed by the same frame, as
/// Flash Player does. Frame scripts and event handlers are C# translations registered by the screen.
/// </summary>
internal sealed class FlashClip : FlashDisplayObject, IFlashPointerTarget
{
    private readonly List<FlashDisplayObject> _children = [];
    private readonly Dictionary<string, object?> _variables = new(StringComparer.OrdinalIgnoreCase);

    internal FlashClip(FlashStage stage, FlashMovie movie, FlashSpriteCharacter sprite, FlashPlacement placement, FlashClip? parent)
        : base(movie, sprite, placement, parent)
    {
        ArgumentNullException.ThrowIfNull(stage);

        Stage = stage;
        Timeline = sprite.Timeline;
        TimelineKey = sprite.Id != 0 ? sprite.Id.ToString(CultureInfo.InvariantCulture) : parent is null ? "root" : "empty";
    }

    public FlashStage Stage
    {
        get;
    }

    public FlashTimeline Timeline
    {
        get; private set;
    }

    /// <summary>"root" for a movie's main timeline, "empty" for a script-created clip, otherwise the sprite's character id; scripts register by it.</summary>
    public string TimelineKey
    {
        get; private set;
    }

    /// <summary>AS2 <c>_currentframe</c> (1-based).</summary>
    public int CurrentFrame
    {
        get; private set;
    }

    /// <summary>AS2 <c>_totalframes</c>.</summary>
    public int TotalFrames => Timeline.FrameCount;

    public bool IsPlaying { get; private set; } = true;

    /// <summary>False once removed from the display list; its queued scripts and handlers no longer run.</summary>
    public bool IsOnStage { get; internal set; } = true;

    public IReadOnlyList<FlashDisplayObject> Children => _children;

    public Action<FlashClip>? OnEnterFrame
    {
        get; set;
    }

    public Action<FlashClip>? OnRollOver
    {
        get; set;
    }

    public Action<FlashClip>? OnRollOut
    {
        get; set;
    }

    public Action<FlashClip>? OnPress
    {
        get; set;
    }

    public Action<FlashClip>? OnRelease
    {
        get; set;
    }

    public Action<FlashClip>? OnReleaseOutside
    {
        get; set;
    }

    public Action<FlashClip>? OnDragOut
    {
        get; set;
    }

    public Action<FlashClip>? OnDragOver
    {
        get; set;
    }

    /// <summary>AS2 <c>onMouseMove</c>, broadcast to every clip on each pointer move.</summary>
    public Action<FlashClip>? OnMouseMove
    {
        get; set;
    }

    /// <summary>AS2 <c>enabled</c>: a disabled clip receives no pointer events.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>A clip that handles the pointer is targeted as a whole; its children are not targeted separately.</summary>
    public bool HandlesPointer => OnRollOver is not null || OnRollOut is not null || OnPress is not null || OnRelease is not null ||
                                  OnReleaseOutside is not null || OnDragOut is not null || OnDragOver is not null;

    /// <summary>
    /// AS2 <c>hitArea</c>. Instance names are properties in AS2, so a child named "hitArea" is the hit area unless a
    /// script assigns another clip.
    /// </summary>
    public FlashClip? HitArea
    {
        get => _hitArea ?? Child("hitArea") as FlashClip;
        set => _hitArea = value;
    }

    private FlashClip? _hitArea;

    /// <summary>AS2 dynamic properties of this clip (variables its scripts define), case-insensitive as in SWF 6.</summary>
    public T? Get<T>(string name) => _variables.TryGetValue(name, out object? value) && value is T typed ? typed : default;

    public void Set(string name, object? value) => _variables[name] = value;

    /// <summary>True when this clip defines the variable, even with an undefined value.</summary>
    public bool TryGet(string name, out object? value) => _variables.TryGetValue(name, out value);

    public FlashDisplayObject? Child(string name)
    {
        foreach (FlashDisplayObject child in _children)
        {
            if (child.Name == name)
            {
                return child;
            }
        }

        return null;
    }

    /// <summary>
    /// AS2 <c>clip.name</c> where the reference may be undefined, as in a handler that outlives the frame holding the
    /// child; calls on an undefined clip do nothing in Flash, so callers use <c>?.</c>.
    /// </summary>
    public FlashClip? ClipOrNull(string name) => Child(name) as FlashClip;

    public FlashClip ChildClip(string name) =>
        Child(name) as FlashClip ?? throw new InvalidOperationException($"Clip '{Name ?? TimelineKey}' has no child clip '{name}'.");

    public FlashTextInstance ChildText(string name) =>
        Child(name) as FlashTextInstance ?? throw new InvalidOperationException($"Clip '{Name ?? TimelineKey}' has no text field '{name}'.");

    public void Play() => IsPlaying = true;

    public void Stop() => IsPlaying = false;

    public void GotoAndPlay(int frame)
    {
        IsPlaying = true;
        Seek(frame);
    }

    public void GotoAndPlay(string label) => GotoAndPlay(FrameOf(label));

    public void GotoAndStop(int frame)
    {
        IsPlaying = false;
        Seek(frame);
    }

    public void GotoAndStop(string label) => GotoAndStop(FrameOf(label));

    /// <summary>AS2 <c>nextFrame()</c>: stops on the next frame (or stays on the last).</summary>
    public void NextFrame()
    {
        IsPlaying = false;
        if (CurrentFrame < TotalFrames)
        {
            Seek(CurrentFrame + 1);
        }
    }

    /// <summary>
    /// AS2 <c>swapDepths(depth)</c>: moves this clip to <paramref name="depth"/> in its parent, exchanging places with
    /// whatever is there. Both then belong to the script, so the timeline no longer removes them.
    /// </summary>
    public void SwapDepths(int depth)
    {
        FlashClip parent = Parent ?? throw new InvalidOperationException("A root timeline has no parent to swap depths in.");
        parent.Swap(this, depth);
    }

    /// <summary>
    /// AS2 <c>createEmptyMovieClip(name, depth)</c>: an empty one-frame clip that scripts drive, e.g. a key poller.
    /// </summary>
    public FlashClip CreateEmptyMovieClip(string name, int depth)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (At(depth) is { } occupant)
        {
            _children.Remove(occupant);
            Stage.Detach(occupant);
        }

        FlashTimeline empty = new([[]], new Dictionary<string, int>());
        FlashClip clip = new(Stage, Movie, new FlashSpriteCharacter(0, empty),
            new FlashPlacement(depth, 0, name, FlashMatrix.Identity, FlashColorTransform.Identity, null, null, CurrentFrame), this)
        {
            ScriptOwnsDepth = true,
        };
        clip.Initialize();
        Insert(clip);
        return clip;
    }

    /// <summary>
    /// AS2 <c>loadMovie</c> of an already baked movie: this clip keeps its name, depth and transform and plays the
    /// other movie's main timeline from frame 1.
    /// </summary>
    public void LoadMovie(FlashMovie movie)
    {
        ArgumentNullException.ThrowIfNull(movie);

        foreach (FlashDisplayObject child in _children)
        {
            Stage.Detach(child);
        }

        _children.Clear();
        _variables.Clear();
        _hitArea = null;
        OnEnterFrame = OnRollOver = OnRollOut = OnPress = OnRelease = OnReleaseOutside = OnDragOut = OnDragOver = OnMouseMove = null;

        FlashSpriteCharacter root = new(0, movie.Root);
        Movie = movie;
        Character = root;
        Timeline = root.Timeline;
        TimelineKey = "root";
        CurrentFrame = 0;
        IsPlaying = true;
        Seek(1);
    }

    void IFlashPointerTarget.RollOver() => Invoke(OnRollOver);

    void IFlashPointerTarget.RollOut() => Invoke(OnRollOut);

    void IFlashPointerTarget.Press() => Invoke(OnPress);

    void IFlashPointerTarget.Release() => Invoke(OnRelease);

    void IFlashPointerTarget.ReleaseOutside() => Invoke(OnReleaseOutside);

    void IFlashPointerTarget.DragOut() => Invoke(OnDragOut);

    void IFlashPointerTarget.DragOver() => Invoke(OnDragOver);

    private void Invoke(Action<FlashClip>? handler)
    {
        if (IsOnStage)
        {
            handler?.Invoke(this);
        }
    }

    /// <summary>One frame of playback: the next frame, or frame 1 after the last.</summary>
    internal void Advance()
    {
        if (!IsPlaying || TotalFrames == 1)
        {
            return;
        }

        Seek(CurrentFrame == TotalFrames ? 1 : CurrentFrame + 1);
    }

    /// <summary>Enters frame 1 of a newly placed clip; the caller queues its scripts in Flash order.</summary>
    internal List<FlashClip> Initialize()
    {
        CurrentFrame = 1;
        return Rebuild(1);
    }

    private int FrameOf(string label) =>
        Timeline.Labels.TryGetValue(label, out int frame)
            ? frame
            : throw new InvalidOperationException($"Clip '{Name ?? TimelineKey}' has no frame label '{label}'.");

    private void Seek(int frame)
    {
        frame = Math.Clamp(frame, 1, TotalFrames);
        if (frame == CurrentFrame)
        {
            return;
        }

        CurrentFrame = frame;
        List<FlashClip> created = Rebuild(frame);
        Stage.QueueFrameScript(this, frame);
        foreach (FlashClip clip in created)
        {
            Stage.QueueInitialScripts(clip);
        }
    }

    private List<FlashClip> Rebuild(int frame)
    {
        IReadOnlyList<FlashPlacement> target = Timeline.DisplayList(frame);
        Dictionary<int, FlashPlacement> byDepth = new(target.Count);
        foreach (FlashPlacement placement in target)
        {
            byDepth.Add(placement.Depth, placement);
        }

        for (int index = _children.Count - 1; index >= 0; index--)
        {
            FlashDisplayObject child = _children[index];
            if (!child.ScriptOwnsDepth && !(byDepth.TryGetValue(child.Depth, out FlashPlacement? placement) && child.IsSameInstance(placement)))
            {
                _children.RemoveAt(index);
                Stage.Detach(child);
            }
        }

        List<FlashClip> created = [];
        foreach (FlashPlacement placement in target)
        {
            FlashDisplayObject? existing = At(placement.Depth);
            if (existing is not null)
            {
                if (!existing.ScriptOwnsDepth)
                {
                    existing.ApplyPlacement(placement);
                }

                continue;
            }

            FlashDisplayObject child = FlashStage.Create(Movie, placement, this);
            Insert(child);
            if (child is FlashClip clip)
            {
                created.Add(clip);
            }
        }

        return created;
    }

    private FlashDisplayObject? At(int depth)
    {
        foreach (FlashDisplayObject child in _children)
        {
            if (child.Depth == depth)
            {
                return child;
            }
        }

        return null;
    }

    private void Insert(FlashDisplayObject child)
    {
        int index = 0;
        while (index < _children.Count && _children[index].Depth < child.Depth)
        {
            index++;
        }

        _children.Insert(index, child);
    }

    private void Swap(FlashDisplayObject child, int depth)
    {
        if (!_children.Remove(child))
        {
            throw new InvalidOperationException("The clip to move is not a child of this clip.");
        }

        FlashDisplayObject? occupant = At(depth);
        if (occupant is not null)
        {
            _children.Remove(occupant);
            occupant.Depth = child.Depth;
            occupant.ScriptOwnsDepth = true;
            Insert(occupant);
        }

        child.Depth = depth;
        child.ScriptOwnsDepth = true;
        Insert(child);
    }
}
