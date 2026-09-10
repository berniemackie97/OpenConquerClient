using System.Security.Cryptography;

namespace OpenConquer.Rendering.Conformance.Support;

internal static class ConformanceHash
{
    public static string Sha256(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    public static string Sha256(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanRead)
        {
            throw new ArgumentException("Stream must be readable.", nameof(stream));
        }

        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
