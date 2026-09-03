namespace OpenConquer.Content.Tool.Import;

/// <summary>
/// Classifies a payload file by its leading magic bytes.
/// </summary>
/// <remarks>
/// Extensions are identity hints only. Recording the observed signature lets verification detect a
/// payload that was replaced with different content under the same name and length.
/// </remarks>
internal static class ContentSignature
{
    /// <summary>Reported when no known magic matches. Not an error: retail ships many bespoke formats.</summary>
    public const string Unknown = "unknown";

    private const int HeaderLength = 12;

    public static string Classify(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith("BM"u8))
        {
            return "bmp";
        }

        if (header.StartsWith("DDS "u8))
        {
            return "dds";
        }

        if (header.StartsWith("RIFF"u8))
        {
            return "riff";
        }

        if (header.StartsWith("PFDW"u8))
        {
            return "wdf";
        }

        if (header.StartsWith("FWS"u8) || header.StartsWith("CWS"u8) || header.StartsWith("ZWS"u8))
        {
            return "swf";
        }

        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return "jpeg";
        }

        return Unknown;
    }

    public static string ClassifyFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        Span<byte> header = stackalloc byte[HeaderLength];

        using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: HeaderLength, FileOptions.SequentialScan);
        int readLength = stream.ReadAtLeast(header, minimumBytes: HeaderLength, throwOnEndOfStream: false);

        return Classify(header[..readLength]);
    }
}
