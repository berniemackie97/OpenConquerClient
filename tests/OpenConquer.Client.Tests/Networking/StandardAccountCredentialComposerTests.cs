using System.Buffers.Binary;
using OpenConquer.Networking.Authentication;

namespace OpenConquer.Client.Tests.Networking;

public sealed class StandardAccountCredentialComposerTests
{
    [Fact]
    public void FullNativeStandardEnvelopeMatchesIndependentCredentialFixture()
    {
        byte[] request = Enumerable.Repeat((byte)0xCC, StandardAccountRequest1060.PacketLength).ToArray();

        StandardAccountCredentialComposer.Write(request, "testacc"u8, "password1".AsSpan(),
            "LocalRealm"u8, 0x12345678u);

        Assert.Equal((ushort)276, BinaryPrimitives.ReadUInt16LittleEndian(request));
        Assert.Equal((ushort)1060, BinaryPrimitives.ReadUInt16LittleEndian(request.AsSpan(2)));
        Assert.Equal("testacc"u8.ToArray(), request.AsSpan(4, 7).ToArray());
        Assert.All(request.AsSpan(11, 121).ToArray(), value => Assert.Equal((byte)0, value));
        Assert.Equal("B059847E5023FEDCD3BCE92F93A9A71008C9ED3654CD597508C9ED3654CD5975",
            Convert.ToHexString(request.AsSpan(StandardAccountRequest1060.CredentialOffset, 32)));
        Assert.All(request.AsSpan(164, 96).ToArray(), value => Assert.Equal((byte)0, value));
        Assert.Equal("LocalRealm"u8.ToArray(), request.AsSpan(260, 10).ToArray());
        Assert.All(request.AsSpan(270, 6).ToArray(), value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public void DifferentSeedsChangeCiphertextButNotAccountAndRealmFields()
    {
        byte[] first = new byte[276];
        byte[] second = new byte[276];
        StandardAccountCredentialComposer.Write(first, "bernie1"u8, "Test1234".AsSpan(), "Realm"u8, 1);
        StandardAccountCredentialComposer.Write(second, "bernie1"u8, "Test1234".AsSpan(), "Realm"u8, 2);

        Assert.NotEqual(first.AsSpan(132, 32).ToArray(), second.AsSpan(132, 32).ToArray());
        Assert.Equal(first.AsSpan(4, 128).ToArray(), second.AsSpan(4, 128).ToArray());
        Assert.Equal(first.AsSpan(260, 16).ToArray(), second.AsSpan(260, 16).ToArray());
    }

    [Fact]
    public void BadInputCannotPartiallyOverwriteRequest()
    {
        byte[] account = "Alice"u8.ToArray();
        byte[] server = "LocalRealm"u8.ToArray();
        byte[] request = Enumerable.Repeat((byte)0xA5, 276).ToArray();

        Assert.Throws<ArgumentException>(() =>
            StandardAccountCredentialComposer.Write(request, account, "!".AsSpan(), server, 10));
        Assert.All(request, value => Assert.Equal((byte)0xA5, value));

        Assert.Throws<ArgumentException>(() =>
            StandardAccountCredentialComposer.Write(request, account, "abc".AsSpan(), new byte[17], 10));
        Assert.All(request, value => Assert.Equal((byte)0xA5, value));

        byte[] undersized = Enumerable.Repeat((byte)0xA5, 275).ToArray();
        Assert.Throws<ArgumentException>(() =>
            StandardAccountCredentialComposer.Write(undersized, account, "abc".AsSpan(), server, 10));
        Assert.All(undersized, value => Assert.Equal((byte)0xA5, value));
    }
}
