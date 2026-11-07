using System.Runtime.ExceptionServices;
using OpenConquer.Client.World;
using OpenConquer.Content.Images;

namespace OpenConquer.Client.UI.Hud.SelectedSkill;

/// <summary>
/// Loads only the current selected skill and its independent cover. One batch is outstanding, and a new selection
/// can supersede the previous result without accumulating work.
/// </summary>
internal sealed class MainHudSelectedSkillImageLoader(Func<string, RgbaImage?> loadSelected, Func<RgbaImage?> loadCover)
    : IAsyncDisposable
{
    private WorldContentLoad<Batch>? _pending;
    private bool _disposed;

    internal MainHudSelectedSkillImageLoader(MainHudSelectedSkillAssets assets)
        : this(section => assets.GetSelectedFrame(section, MainHudSelectedSkillDefinition.FrameIndex), assets.GetCoverFrame)
    {
    }

    internal (ImageResult Selected, ImageResult Cover) Poll(string? section, bool needCover)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ImageResult selected = default;
        ImageResult cover = default;
        if (_pending is { IsCompleted: true } pending)
        {
            _pending = null;
            Batch ready = pending.Complete();
            if (section is not null && string.Equals(ready.Section, section, StringComparison.Ordinal))
            {
                selected = ready.Selected;
            }

            if (needCover)
            {
                cover = ready.Cover;
            }
        }

        string? nextSection = selected.IsComplete ? null : section;
        bool nextCover = needCover && !cover.IsComplete;
        if (_pending is null && (nextSection is not null || nextCover))
        {
            _pending = new WorldContentLoad<Batch>(token => Load(nextSection, nextCover, token));
        }

        return (selected, cover);
    }

    internal void Cancel() => _pending?.Cancel();

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_pending is { } pending)
        {
            _pending = null;
            await pending.DisposeAsync().ConfigureAwait(false);
        }
    }

    private Batch Load(string? section, bool needCover, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        ImageResult selected = section is null ? default : Read(() => loadSelected(section));
        token.ThrowIfCancellationRequested();
        ImageResult cover = needCover ? Read(loadCover) : default;
        return new Batch(section, selected, cover);
    }

    private static ImageResult Read(Func<RgbaImage?> read)
    {
        try
        {
            return new ImageResult(true, read(), null);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            // A failure follows the requested image and is reported only if the image is still wanted.
            return new ImageResult(true, null, ExceptionDispatchInfo.Capture(exception));
        }
    }

    internal readonly record struct ImageResult(bool IsComplete, RgbaImage? Image, ExceptionDispatchInfo? Failure)
    {
        internal RgbaImage? GetImage()
        {
            Failure?.Throw();
            return Image;
        }
    }

    private readonly record struct Batch(string? Section, ImageResult Selected, ImageResult Cover);
}
