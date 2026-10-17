using System.Security.Cryptography;
using OpenConquer.Content.Images;
using OpenConquer.Content.Login;

namespace OpenConquer.Content.Tests.Login;

public sealed class LoginServerIconArtworkTests
{
    [Fact]
    public void EntireRetailImageSet_PreservesDecodedBytesAndNativeDimensions()
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        int count = 0;

        for (int group = 1; group <= 9; group++)
        {
            int servers = group == 2 ? 13 : group == 9 ? 5 : 12;

            for (int server = 1; server <= servers; server++)
            {
                string id = $"g{group:00}s{server:00}";
                Assert.True(LoginServerIconArtwork.IsKnownId(id));
                RgbaImage image = LoginServerIconArtwork.Load(id);
                (int width, int height) = id == "g09s01" ? (164, 85) : (170, 90);
                Assert.Equal(width, image.Width);
                Assert.Equal(height, image.Height);
                Assert.Equal(width * height * 4, image.Pixels.Length);
                hash.AppendData(image.Pixels.Span);
                count++;
            }
        }

        Assert.Equal(LoginServerIconArtwork.IconCount, count);
        Assert.Equal("c57d20964dc906cd5073f4094d1b91dd3cb7c9461fcad52cd4bb89fc4d45392c", Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
        Assert.Equal(count, typeof(LoginServerIconArtwork).Assembly.GetManifestResourceNames().Count(name =>
            name.StartsWith("Assets/Login/ServerIcons/", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("../../secrets")]
    [InlineData("g00s01")]
    [InlineData("g10s01")]
    [InlineData("g01s13")]
    [InlineData("g09s06")]
    [InlineData("G01S01")]
    [InlineData("g01s1")]
    [InlineData("none")]
    public void UnknownOrPathlikeIdentifiersAreRejected(string id)
    {
        Assert.False(LoginServerIconArtwork.IsKnownId(id));
        Assert.Throws<ArgumentException>(() => LoginServerIconArtwork.Load(id));
    }
}
