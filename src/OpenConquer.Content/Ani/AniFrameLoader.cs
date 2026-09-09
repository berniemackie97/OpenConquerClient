using OpenConquer.Content.Images;

namespace OpenConquer.Content.Ani;

/// <summary>
/// Resolves and decodes one frame from a retail ANI index.
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

        if (frameIndex >= section.FrameCount)
        {
            throw new ArgumentOutOfRangeException(nameof(frameIndex), frameIndex, $"ANI section [{sectionName}] contains {section.FrameCount} frame(s).");
        }

        string frameContentPath = section.FramePaths[frameIndex];

        if (!frameContentPath.EndsWith(".tga", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"ANI section [{sectionName}] frame {frameIndex} references unsupported image '{frameContentPath}'.");
        }

        return TargaImageLoader.Load(contentSource, frameContentPath, mode);
    }
}
