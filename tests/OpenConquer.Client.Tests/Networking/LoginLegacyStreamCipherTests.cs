using OpenConquer.Networking.Authentication;

namespace OpenConquer.Client.Tests.Networking;

public sealed class LoginLegacyStreamCipherTests
{
    [Fact]
    public void Outbound_MatchesTheAccountServerInboundVector()
    {
        LoginLegacyStreamCipher cipher = new();
        byte[] frame = Convert.FromHexString("0800230478563412");

        cipher.EncryptOutbound(frame);

        // Independently asserted in OpenConquerServer's LoginStreamCipherTests.
        Assert.Equal(Convert.FromHexString("D48487651720B0C3"), frame);
    }

    [Fact]
    public void Inbound_DecodesTheAccountServerSeedVector()
    {
        LoginLegacyStreamCipher cipher = new();
        byte[] encrypted = Convert.FromHexString("C54869128E317C0F");

        cipher.DecryptInbound(encrypted);

        Assert.Equal(Convert.FromHexString("0800230478563412"), encrypted);
        Assert.True(LoginSeed1059.TryReadSeed(encrypted, out uint seed));
        Assert.Equal(0x12345678u, seed);
    }

    [Fact]
    public void FragmentedWritesUseExactlyTheSameKeystreamAsOneWrite()
    {
        byte[] input = Enumerable.Range(0, 300).Select(i => unchecked((byte)i)).ToArray();
        byte[] fragmented = input.ToArray();
        LoginLegacyStreamCipher whole = new();
        LoginLegacyStreamCipher split = new();

        whole.EncryptOutbound(input);
        split.EncryptOutbound(fragmented.AsSpan(0, 1));
        split.EncryptOutbound(fragmented.AsSpan(1, 254));
        split.EncryptOutbound(fragmented.AsSpan(255, 2));
        split.EncryptOutbound(fragmented.AsSpan(257));

        Assert.Equal(input, fragmented);
    }

    [Fact]
    public void SendAndReceiveStreamPositionsAreIndependent()
    {
        LoginLegacyStreamCipher cipher = new();
        byte[] send = [0];
        byte[] receive = [0];

        cipher.EncryptOutbound(send);
        cipher.DecryptInbound(receive);

        Assert.Equal((byte)0x54, send[0]);
        Assert.Equal(send, receive);
    }

    [Fact]
    public void NativeCountersCarryAndWrapAfterTheFullSixteenBitRange()
    {
        LoginLegacyStreamCipher cipher = new();
        byte[] a = new byte[ushort.MaxValue + 1];
        cipher.EncryptOutbound(a);
        byte[] wrapped = [0];

        cipher.EncryptOutbound(wrapped);

        Assert.Equal((byte)0x54, a[0]);
        Assert.Equal((byte)0x6E, a[255]);
        Assert.Equal((byte)0x86, a[256]);
        Assert.Equal((byte)0x56, a[257]);
        Assert.Equal(a[0], wrapped[0]);
    }

    [Fact]
    public void EmptyInputDoesNotConsumeStreamPosition()
    {
        LoginLegacyStreamCipher cipher = new();
        cipher.DecryptInbound(Span<byte>.Empty);
        byte[] first = [0];
        cipher.DecryptInbound(first);
        Assert.Equal((byte)0x54, first[0]);
    }
}
