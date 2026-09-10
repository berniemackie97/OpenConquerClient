using OpenConquer.Rendering.Conformance.Support;

namespace OpenConquer.Rendering.Conformance.Reference;

internal static class SpriteRotationReference
{
    public const int TargetWidth = 8;
    public const int TargetHeight = 6;

    public static byte[] CreateExpectedFramebuffer()
    {
        byte[] expected = FramebufferVerifier.CreateOpaqueBlack(TargetWidth, TargetHeight);

        for (int y = 1; y < 5; y++)
        {
            bool red = y < 3;

            for (int x = 3; x < 5; x++)
            {
                int offset = ((y * TargetWidth) + x) * 4;

                expected[offset] = red ? byte.MaxValue : (byte)0;
                expected[offset + 1] = red ? (byte)0 : byte.MaxValue;
                expected[offset + 2] = 0;
            }
        }

        return expected;
    }
}
