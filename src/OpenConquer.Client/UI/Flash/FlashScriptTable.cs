namespace OpenConquer.Client.UI.Flash;

/// <summary>
/// C# translations of a movie's frame scripts (DoAction), keyed by movie name, timeline ("root" or a sprite's
/// character id) and frame. A clip runs its frame's script each time it enters that frame.
/// </summary>
internal sealed class FlashScriptTable
{
    private readonly Dictionary<(string Movie, string Timeline, int Frame), Action<FlashClip>> _scripts = [];

    public void Frame(string movie, string timeline, int frame, Action<FlashClip> script)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(movie);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeline);
        ArgumentOutOfRangeException.ThrowIfLessThan(frame, 1);
        ArgumentNullException.ThrowIfNull(script);

        if (!_scripts.TryAdd((movie, timeline, frame), script))
        {
            throw new InvalidOperationException($"Movie '{movie}' timeline {timeline} frame {frame} already has a script.");
        }
    }

    /// <summary>Registers the same script for every listed timeline, e.g. the shared <c>stop()</c> of many clips.</summary>
    public void Frame(string movie, IEnumerable<string> timelines, int frame, Action<FlashClip> script)
    {
        ArgumentNullException.ThrowIfNull(timelines);

        foreach (string timeline in timelines)
        {
            Frame(movie, timeline, frame, script);
        }
    }

    internal Action<FlashClip>? Find(string movie, string timeline, int frame) =>
        _scripts.TryGetValue((movie, timeline, frame), out Action<FlashClip>? script) ? script : null;
}
