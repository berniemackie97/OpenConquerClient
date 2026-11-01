using OpenConquer.Content.Ani;

namespace OpenConquer.Content.Tool.World;

internal sealed class WorldFrameValidation(IClientContentSource source)
{
    private readonly HashSet<string> _decodedPaths = new(StringComparer.OrdinalIgnoreCase);

    public int FrameCount => _decodedPaths.Count;

    public void Validate(AniIndexSection section)
    {
        for (int frame = 0; frame < section.FrameCount; frame++)
        {
            if (_decodedPaths.Add(section.FramePaths[frame]))
            {
                _ = AniFrameLoader.LoadWrappedFrame(source, section, (uint)frame,
                    ContentLookupMode.LooseThenPackage);
            }
        }
    }
}
