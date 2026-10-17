using System.Security.Cryptography;
using OpenConquer.Content.Images;
using OpenConquer.Content.Login;

namespace OpenConquer.Content.Tests.Login;

public sealed class LoginCursorArtworkTests
{
    [Fact]
    public void NativeAnimatedCursorHasFiveOriginalFramesAndHotspot()
    {
        string[] hashes =
        [
            "21b4788930eb5f60b1bc0b904d4724e04a828505d9a5021fc6867d28d60c47d7",
            "eb2b3defab9c823c8727b61259f6c3004fd83adb3b7813be2b5770836bcefc75",
            "4fdfd09bd5a4744789d255512a877c62ad4d35308e0cbc9d521fd71ae3c904de",
            "8b7af6a11e4b0765f227254810d389d7d3b26b838ad2ce15df59faabc38f6e05",
            "54c287eaa5fc69b4c9838bebbd2215f06e95be9eb045f470251f924e69c800be",
        ];
        IReadOnlyList<RgbaImage> frames = LoginCursorArtwork.Load();
        Assert.Equal(5, frames.Count);
        Assert.Equal(0, LoginCursorArtwork.HotspotX);
        Assert.Equal(0, LoginCursorArtwork.HotspotY);
        Assert.Equal(TimeSpan.FromSeconds(10d / 60d), LoginCursorArtwork.FrameDuration);
        for (int i = 0; i < frames.Count; i++)
        {
            Assert.Equal(32, frames[i].Width);
            Assert.Equal(32, frames[i].Height);
            Assert.Equal(4096, frames[i].Pixels.Length);
            Assert.Equal(hashes[i], Convert.ToHexStringLower(SHA256.HashData(frames[i].Pixels.Span)));
        }
    }
}
