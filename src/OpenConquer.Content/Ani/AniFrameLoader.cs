using OpenConquer.Content.Images;

namespace OpenConquer.Content.Ani;

/// <summary>
/// Resolves and decodes frames from retail ANI indexes.
/// </summary>
public static class AniFrameLoader
{
    public static RgbaImage Load(IClientContentSource contentSource, string aniContentPath, string sectionName, int frameIndex, ContentLookupMode mode)
    {
        ArgumentNullException.ThrowIfNull(contentSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(aniContentPath);
        ArgumentException.ThrowIfNullOrEmpty(sectionName);
        ArgumentOutOfRangeException.ThrowIfNegative(frameIndex);

        AniIndexSection section = AniIndexFile.Load(contentSource, aniContentPath, mode).GetRequiredSection(sectionName);

        return LoadFrame(contentSource, section, frameIndex, mode);
    }

    internal static RgbaImage LoadFrame(IClientContentSource contentSource, AniIndexSection section, int frameIndex, ContentLookupMode mode)
    {
        ArgumentNullException.ThrowIfNull(contentSource);
        ArgumentNullException.ThrowIfNull(section);
        ArgumentOutOfRangeException.ThrowIfNegative(frameIndex);

        if (frameIndex >= section.FrameCount)
        {
            throw new ArgumentOutOfRangeException(nameof(frameIndex), frameIndex, $"ANI section [{section.Name}] contains {section.FrameCount} frame(s).");
        }

        string frameContentPath = section.FramePaths[frameIndex];

        if (!frameContentPath.EndsWith(".tga", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"ANI section [{section.Name}] frame {frameIndex} references unsupported image '{frameContentPath}'.");
        }

        return TargaImageLoader.Load(contentSource, frameContentPath, mode);
    }
}
