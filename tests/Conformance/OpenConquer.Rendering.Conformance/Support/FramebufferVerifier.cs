namespace OpenConquer.Rendering.Conformance.Support;

internal static class FramebufferVerifier
{
    public static void VerifyExact(string caseName, ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseName);

        if (actual.SequenceEqual(expected))
        {
            return;
        }

        string expectedHash = ConformanceHash.Sha256(expected);
        string actualHash = ConformanceHash.Sha256(actual);

        throw new InvalidDataException($"{caseName} framebuffer SHA256 {actualHash} does not match the independently composed expected framebuffer {expectedHash}.");
    }

    public static byte[] CreateOpaqueBlack(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        byte[] framebuffer = new byte[checked(width * height * 4)];

        for (int offset = 3; offset < framebuffer.Length; offset += 4)
        {
            framebuffer[offset] = byte.MaxValue;
        }

        return framebuffer;
    }
}
