using System.Text;
using OpenConquer.Networking.Authentication;

namespace OpenConquer.Client.Tests.Networking;

public sealed class LoginPasswordKeypadEncoderTests
{
    [Theory]
    [InlineData("testacc", "password1", "2165AAAA5AC76B7E92")]
    [InlineData("bernie1", "Test1234", "75F9ECF5694F64B3")]
    [InlineData("abc", "a-2.z", "686346F547")]
    [InlineData("Alice", "abc", "C6F1F5")]
    [InlineData("Alice", "1", "C4")]
    [InlineData("Alice", "-", "17")]
    [InlineData("Alice", "_", "97")]
    public void EncodeMatchesNativeCompatibleServerReferenceVectors(string account, string password, string expectedHex)
    {
        byte[] result = Enumerable.Repeat((byte)0xCC, LoginPasswordKeypadEncoder.EncodedFieldLength).ToArray();

        LoginPasswordKeypadEncoder.Encode(Encoding.ASCII.GetBytes(account), password.AsSpan(), result);

        byte[] expected = Convert.FromHexString(expectedHex);
        Assert.Equal(expected, result.AsSpan(0, expected.Length).ToArray());
        Assert.All(result.AsSpan(expected.Length).ToArray(), value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public void AccountSeedUsesSignedByteSum()
    {
        byte[] output = new byte[LoginPasswordKeypadEncoder.EncodedFieldLength];

        LoginPasswordKeypadEncoder.Encode([0xE9], "abc1".AsSpan(), output);

        Assert.Equal("8515B489", Convert.ToHexString(output.AsSpan(0, 4)));
        Assert.All(output[4..], value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public void UnsupportedOrOverlongPasswordDoesNotChangeDestination()
    {
        byte[] account = "Alice"u8.ToArray();
        foreach (string password in new[] { "", "hello!", "not/allowed", "unicodeé", new string('a', 16) })
        {
            byte[] output = Enumerable.Repeat((byte)0xA5, 32).ToArray();
            Assert.Throws<ArgumentException>(() => LoginPasswordKeypadEncoder.Encode(account, password.AsSpan(), output));
            Assert.All(output, value => Assert.Equal((byte)0xA5, value));
        }
    }

    [Fact]
    public void InvalidAccountOrDestinationNeverModifiesDestination()
    {
        foreach (byte[] account in new byte[][] { [], [0], [(byte)'A', 0x7F], new byte[33] })
        {
            byte[] output = Enumerable.Repeat((byte)0xA5, 32).ToArray();
            Assert.Throws<ArgumentException>(() => LoginPasswordKeypadEncoder.Encode(account, "abc".AsSpan(), output));
            Assert.All(output, value => Assert.Equal((byte)0xA5, value));
        }

        byte[] shortOutput = Enumerable.Repeat((byte)0xA5, 31).ToArray();
        Assert.Throws<ArgumentException>(() => LoginPasswordKeypadEncoder.Encode("Alice"u8, "abc".AsSpan(), shortOutput));
        Assert.All(shortOutput, value => Assert.Equal((byte)0xA5, value));
    }
}
