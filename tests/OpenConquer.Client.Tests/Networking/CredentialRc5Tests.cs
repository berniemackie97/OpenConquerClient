using OpenConquer.Networking.Authentication;

namespace OpenConquer.Client.Tests.Networking;

public sealed class CredentialRc5Tests
{
    [Theory]
    [InlineData(0u, "2627F6859715AD1DD294DDC476193931")]
    [InlineData(1u, "2923BE84E16CD6AE529049F1F1BBE9EB")]
    [InlineData(0x12345678u, "E93F0DA196953104492D9E6183CF096F")]
    [InlineData(uint.MaxValue, "232B2E854CBE858D52987198FB778977")]
    public void LoginSeedKeyMatchesIndependentMsvcrtReference(uint seed, string expected)
    {
        byte[] derived = new byte[LoginSeedKey.KeyLength];
        LoginSeedKey.Derive(seed, derived);
        Assert.Equal(expected, Convert.ToHexString(derived));
    }

    [Fact]
    public void Rc5ZeroKeyZeroBlockMatchesPublished32_12_16Vector()
    {
        byte[] output = new byte[CredentialRc5.BlockLength];
        CredentialRc5.EncryptBlock(new byte[16], new byte[8], output);
        Assert.Equal("21A5DBEE154B8F6D", Convert.ToHexString(output));
    }

    [Theory]
    [InlineData(0u, "ADF886F4609B492E")]
    [InlineData(1u, "39E02F179EE1E80D")]
    [InlineData(0x12345678u, "08C9ED3654CD5975")]
    [InlineData(uint.MaxValue, "318E49D877F1D124")]
    public void StandardCredentialCipherMatchesIndependentMsvcrtRc5Reference(uint seed, string expectedBlock)
    {
        byte[] output = Enumerable.Repeat((byte)0xCC, CredentialRc5.StandardFieldLength).ToArray();
        CredentialRc5.EncryptStandard(seed, new byte[CredentialRc5.StandardFieldLength], output);
        Assert.Equal(string.Concat(Enumerable.Repeat(expectedBlock, 4)), Convert.ToHexString(output));
    }

    [Fact]
    public void StandardCipherReadsOverlappingInputBeforeWritingEncryptedOutput()
    {
        byte[] source = Enumerable.Range(0, 32).Select(index => (byte)index).ToArray();
        byte[] expected = new byte[32];
        CredentialRc5.EncryptStandard(0x12345678u, source, expected);

        byte[] overlapping = source.ToArray();
        CredentialRc5.EncryptStandard(0x12345678u, overlapping, overlapping);
        Assert.Equal(expected, overlapping);
        Assert.NotEqual(source, overlapping);

        byte[] shared = new byte[64];
        source.CopyTo(shared, 0);
        CredentialRc5.EncryptStandard(0x12345678u, shared.AsSpan(0, 32), shared.AsSpan(16, 32));
        Assert.Equal(expected, shared.AsSpan(16, 32).ToArray());
    }

    [Fact]
    public void InvalidDimensionsNeverWriteDestination()
    {
        byte[] destination = Enumerable.Repeat((byte)0xA5, 32).ToArray();

        foreach (int length in new[] { 0, 8, 31, 33, 128 })
        {
            byte[] invalid = new byte[length];
            Assert.Throws<ArgumentException>(() => CredentialRc5.EncryptStandard(7, invalid, destination));
            Assert.All(destination, b => Assert.Equal((byte)0xA5, b));
        }

        byte[] valid = new byte[32];
        byte[] wrongDestination = new byte[31];
        Assert.Throws<ArgumentException>(() => CredentialRc5.EncryptStandard(7, valid, wrongDestination));
        Assert.Throws<ArgumentException>(() => CredentialRc5.EncryptBlock(new byte[15], new byte[8], new byte[8]));
        Assert.Throws<ArgumentException>(() => CredentialRc5.EncryptBlock(new byte[16], new byte[7], new byte[8]));
        Assert.Throws<ArgumentException>(() => CredentialRc5.EncryptBlock(new byte[16], new byte[8], new byte[7]));
        Assert.Throws<ArgumentException>(() => LoginSeedKey.Derive(7, new byte[15]));
        Assert.All(destination, b => Assert.Equal((byte)0xA5, b));
    }

    [Fact]
    public void Existing1060EnvelopeCanCarryTheEncryptedBlockWithoutEnablingNetworkOrLogin()
    {
        byte[] ciphertext = new byte[CredentialRc5.StandardFieldLength];
        CredentialRc5.EncryptStandard(0x12345678u, new byte[32], ciphertext);
        byte[] packet = new byte[StandardAccountRequest1060.PacketLength];

        StandardAccountRequest1060.Write(packet, "Account"u8, ciphertext, "LocalRealm"u8);

        Assert.Equal(ciphertext, packet.AsSpan(StandardAccountRequest1060.CredentialOffset, 32).ToArray());
        Assert.All(packet.AsSpan(StandardAccountRequest1060.CredentialOffset + 32, 96).ToArray(), value => Assert.Equal((byte)0, value));
    }
}
