using System.Buffers.Binary;
using OpenConquer.Networking.Authentication;

namespace OpenConquer.Client.Tests.Networking;

public sealed class StandardAccountRequest1060Tests
{
    [Fact]
    public void Write_ProducesNative276ByteEnvelopeAndPreservesBinaryCiphertext()
    {
        byte[] output = Enumerable.Repeat((byte)0xCC, StandardAccountRequest1060.PacketLength).ToArray();
        byte[] encrypted = Enumerable.Range(0, 32).Select(index => (byte)index).ToArray();

        StandardAccountRequest1060.Write(output, "account"u8, encrypted, "Dark"u8);

        Assert.Equal((ushort)276, BinaryPrimitives.ReadUInt16LittleEndian(output));
        Assert.Equal((ushort)1060, BinaryPrimitives.ReadUInt16LittleEndian(output.AsSpan(2)));
        Assert.Equal("account"u8.ToArray(), output.AsSpan(4, 7).ToArray());
        Assert.All(output[11..132], character => Assert.Equal((byte)0, character));
        Assert.Equal(encrypted, output.AsSpan(132, 32).ToArray());
        Assert.All(output[164..260], character => Assert.Equal((byte)0, character));
        Assert.Equal("Dark"u8.ToArray(), output.AsSpan(260, 4).ToArray());
        Assert.All(output[264..276], character => Assert.Equal((byte)0, character));
    }

    [Fact]
    public void Write_PreservesFullWidthWireFieldsAndHighAnsiBytes()
    {
        byte[] account = Enumerable.Repeat((byte)'A', 128).ToArray();
        byte[] server = Enumerable.Repeat((byte)0x81, 16).ToArray();
        byte[] encrypted = Enumerable.Repeat((byte)0xE5, 32).ToArray();
        byte[] output = new byte[276];

        StandardAccountRequest1060.Write(output, account, encrypted, server);

        Assert.Equal(account, output.AsSpan(4, 128).ToArray());
        Assert.Equal(encrypted, output.AsSpan(132, 32).ToArray());
        Assert.Equal(server, output.AsSpan(260, 16).ToArray());
    }

    [Fact]
    public void Write_AcceptsOverlappingSourceAndDestinationWithoutCorruption()
    {
        byte[] output = new byte[276];
        "Alice"u8.CopyTo(output.AsSpan(64));
        "Realm"u8.CopyTo(output.AsSpan(240));
        for (int index = 0; index < 32; index++)
        {
            output[132 + index] = (byte)(0x80 + index);
        }

        StandardAccountRequest1060.Write(output, output.AsSpan(64, 5), output.AsSpan(132, 32), output.AsSpan(240, 5));

        Assert.Equal("Alice"u8.ToArray(), output.AsSpan(4, 5).ToArray());
        Assert.Equal("Realm"u8.ToArray(), output.AsSpan(260, 5).ToArray());
        Assert.Equal(Enumerable.Range(0, 32).Select(index => (byte)(0x80 + index)).ToArray(), output.AsSpan(132, 32).ToArray());
    }

    [Fact]
    public void Write_RepeatedUseCannotLeakStaleOutputBytes()
    {
        byte[] output = Enumerable.Repeat((byte)0xFF, 276).ToArray();
        byte[] encrypted = new byte[32];

        StandardAccountRequest1060.Write(output, "LongAccount"u8, encrypted, "LongRealm"u8);
        StandardAccountRequest1060.Write(output, "A"u8, encrypted, "B"u8);

        Assert.Equal((byte)'A', output[4]);
        Assert.All(output[5..132], value => Assert.Equal((byte)0, value));
        Assert.Equal((byte)'B', output[260]);
        Assert.All(output[261..276], value => Assert.Equal((byte)0, value));
    }

    [Theory]
    [InlineData(275)]
    [InlineData(277)]
    [InlineData(0)]
    public void Write_RejectsIncorrectDestinationWithoutMutation(int length)
    {
        byte[] output = Enumerable.Repeat((byte)0xA5, length).ToArray();
        byte[] before = output.ToArray();
        byte[] encrypted = new byte[32];

        byte[] account = "Alice"u8.ToArray();
        byte[] server = "Realm"u8.ToArray();
        Assert.Throws<ArgumentException>(() => StandardAccountRequest1060.Write(output, account, encrypted, server));
        Assert.Equal(before, output);
    }

    [Fact]
    public void Write_InvalidInputsDoNotModifyExistingDestination()
    {
        byte[] output = Enumerable.Repeat((byte)0x5A, 276).ToArray();
        byte[] encrypted = new byte[32];
        byte[] goodAccount = "Alice"u8.ToArray();
        byte[] goodServer = "Realm"u8.ToArray();
        byte[][] badAccounts = [[], new byte[129], [(byte)'A', 0, (byte)'B'], [(byte)'A', 0x0A]];
        byte[][] badServers = [[], new byte[17], [(byte)'S', 0], [(byte)'S', 0x7F]];

        foreach (byte[] account in badAccounts)
        {
            Assert.Throws<ArgumentException>(() => StandardAccountRequest1060.Write(output, account, encrypted, goodServer));
            Assert.All(output, value => Assert.Equal((byte)0x5A, value));
        }

        foreach (byte[] server in badServers)
        {
            Assert.Throws<ArgumentException>(() => StandardAccountRequest1060.Write(output, goodAccount, encrypted, server));
            Assert.All(output, value => Assert.Equal((byte)0x5A, value));
        }

        foreach (int length in new[] { 0, 31, 33, 128 })
        {
            byte[] malformedBlock = new byte[length];
            Assert.Throws<ArgumentException>(() => StandardAccountRequest1060.Write(output, goodAccount, malformedBlock, goodServer));
            Assert.All(output, value => Assert.Equal((byte)0x5A, value));
        }
    }
}
