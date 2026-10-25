using System.Numerics;
using OpenConquer.Content.Flash;

namespace OpenConquer.Client.UI.Flash;

/// <summary>
/// Plays one Flash movie and the movies loaded into it. Each <see cref="Tick"/> is one Flash frame: every clip's
/// <c>onEnterFrame</c> runs, then every playing clip advances, then the frame scripts this queued run in order
/// (a parent's before those of the children it placed, as in AVM1). Pointer input targets the topmost button or
/// clip with pointer handlers under the pointer.
/// </summary>
internal sealed class FlashStage
{
    private readonly Queue<(FlashClip Clip, int Frame)> _pendingScripts = new();
    private readonly HashSet<int> _keysDown = [];
    private readonly FlashHitTester _hitTester;

    private IFlashPointerTarget? _hovered;
    private IFlashPointerTarget? _pressed;
    private bool _started;

    public FlashStage(FlashMovie movie, FlashScriptTable scripts, IFlashRasterSource rasters)
    {
        ArgumentNullException.ThrowIfNull(movie);
        ArgumentNullException.ThrowIfNull(scripts);
        ArgumentNullException.ThrowIfNull(rasters);

        Scripts = scripts;
        _hitTester = new FlashHitTester(rasters);
        Root = new FlashClip(this, movie, new FlashSpriteCharacter(0, movie.Root),
            new FlashPlacement(0, 0, null, FlashMatrix.Identity, FlashColorTransform.Identity, null, null, 1), parent: null);
    }

    public FlashClip Root
    {
        get;
    }

    public FlashScriptTable Scripts
    {
        get;
    }

    /// <summary>The pointer in stage pixels.</summary>
    public Vector2 Pointer
    {
        get; private set;
    }

    /// <summary>Enters the root's frame 1 and runs its scripts. Called once, before the first <see cref="Tick"/>.</summary>
    public void Start()
    {
        if (_started)
        {
            throw new InvalidOperationException("The Flash stage has already started.");
        }

        _started = true;
        List<FlashClip> created = Root.Initialize();
        QueueFrameScript(Root, 1);
        foreach (FlashClip clip in created)
        {
            QueueInitialScripts(clip);
        }

        RunPendingScripts();
    }

    public void Tick()
    {
        EnsureStarted();

        // Work queued outside a script (a screen calling loadMovie or gotoAndPlay) runs before the frame advances.
        RunPendingScripts();

        foreach (FlashClip clip in Clips())
        {
            if (clip.IsOnStage)
            {
                clip.OnEnterFrame?.Invoke(clip);
            }
        }

        foreach (FlashClip clip in Clips())
        {
            if (clip.IsOnStage)
            {
                clip.Advance();
            }
        }

        RunPendingScripts();
    }

    /// <summary>AS2 <c>_xmouse</c>/<c>_ymouse</c> of <paramref name="clip"/>: the pointer in its coordinate space.</summary>
    public Vector2 LocalPointer(FlashDisplayObject clip)
    {
        ArgumentNullException.ThrowIfNull(clip);

        return Matrix3x2.Invert(FlashTransforms.World(clip), out Matrix3x2 inverse) ? Vector2.Transform(Pointer, inverse) : Vector2.Zero;
    }

    /// <summary>AS2 <c>Key.isDown</c> by virtual key code (13 Enter, 27 Escape).</summary>
    public bool IsKeyDown(int keyCode) => _keysDown.Contains(keyCode);

    public void SetKey(int keyCode, bool down)
    {
        if (down)
        {
            _keysDown.Add(keyCode);
        }
        else
        {
            _keysDown.Remove(keyCode);
        }
    }

    public void PointerMoved(Vector2 position)
    {
        EnsureStarted();
        Pointer = position;

        foreach (FlashClip clip in Clips())
        {
            if (clip.IsOnStage)
            {
                clip.OnMouseMove?.Invoke(clip);
            }
        }

        UpdateHover();
        RunPendingScripts();
    }

    public void PointerPressed()
    {
        EnsureStarted();
        UpdateHover();

        if (_hovered is not null)
        {
            _pressed = _hovered;
            _pressed.Press();
        }

        RunPendingScripts();
    }

    public void PointerReleased()
    {
        EnsureStarted();

        if (_pressed is { } pressed)
        {
            _pressed = null;
            IFlashPointerTarget? under = _hitTester.Target(Root, Pointer);
            if (ReferenceEquals(under, pressed))
            {
                pressed.Release();
            }
            else
            {
                // Flash sends releaseOutside and no rollOut to a target the pointer left while pressed.
                pressed.ReleaseOutside();
                _hovered = null;
            }
        }

        UpdateHover();
        RunPendingScripts();
    }

    internal static FlashDisplayObject Create(FlashMovie movie, FlashPlacement placement, FlashClip parent) =>
        movie.Character(placement.CharacterId) switch
        {
            FlashSpriteCharacter sprite => CreateClip(movie, sprite, placement, parent),
            FlashEditTextCharacter text => new FlashTextInstance(movie, text, placement, parent),
            FlashButtonCharacter button => new FlashButtonInstance(movie, button, placement, parent),
            FlashCharacter shape => new FlashShapeInstance(movie, shape, placement, parent),
        };

    internal void Detach(FlashDisplayObject removed)
    {
        if (removed is FlashClip clip)
        {
            clip.IsOnStage = false;
            foreach (FlashDisplayObject child in clip.Children)
            {
                Detach(child);
            }
        }

        if (ReferenceEquals(_hovered, removed))
        {
            _hovered = null;
        }

        if (ReferenceEquals(_pressed, removed))
        {
            _pressed = null;
        }
    }

    internal void QueueFrameScript(FlashClip clip, int frame) => _pendingScripts.Enqueue((clip, frame));

    internal void QueueInitialScripts(FlashClip clip)
    {
        QueueFrameScript(clip, 1);
        foreach (FlashDisplayObject child in clip.Children)
        {
            if (child is FlashClip nested)
            {
                QueueInitialScripts(nested);
            }
        }
    }

    private static FlashClip CreateClip(FlashMovie movie, FlashSpriteCharacter sprite, FlashPlacement placement, FlashClip parent)
    {
        FlashClip clip = new(parent.Stage, movie, sprite, placement, parent);
        clip.Initialize();
        return clip;
    }

    private void RunPendingScripts()
    {
        while (_pendingScripts.TryDequeue(out (FlashClip Clip, int Frame) pending))
        {
            // A script runs only if its clip is still on that frame and still on stage.
            if (pending.Clip.IsOnStage && pending.Clip.CurrentFrame == pending.Frame)
            {
                Scripts.Find(pending.Clip.Movie.Name, pending.Clip.TimelineKey, pending.Frame)?.Invoke(pending.Clip);
            }
        }
    }

    private void UpdateHover()
    {
        if (_pressed is not null)
        {
            return;
        }

        IFlashPointerTarget? target = _hitTester.Target(Root, Pointer);
        if (ReferenceEquals(target, _hovered))
        {
            return;
        }

        IFlashPointerTarget? previous = _hovered;
        _hovered = target;
        previous?.RollOut();
        target?.RollOver();
    }

    private List<FlashClip> Clips()
    {
        List<FlashClip> clips = [];
        Collect(Root, clips);
        return clips;

        static void Collect(FlashClip clip, List<FlashClip> clips)
        {
            clips.Add(clip);
            foreach (FlashDisplayObject child in clip.Children)
            {
                if (child is FlashClip nested)
                {
                    Collect(nested, clips);
                }
            }
        }
    }

    private void EnsureStarted()
    {
        if (!_started)
        {
            throw new InvalidOperationException("Start the Flash stage before driving it.");
        }
    }
}
