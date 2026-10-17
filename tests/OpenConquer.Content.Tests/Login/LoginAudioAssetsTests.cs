using System.Security.Cryptography;
using OpenConquer.Content.Login;

namespace OpenConquer.Content.Tests.Login;

public sealed class LoginAudioAssetsTests
{
    [Theory]
    [InlineData(LoginAudioCue.Music, 44100, 2, 5299200, "9512fcb22755239fa39ce88e95a2a805bd17d5e8a5b2414754382145283a9e67")]
    [InlineData(LoginAudioCue.Glide, 22050, 1, 11520, "e93e7bb99d8d2c9b87b696ee8247b0e45df90d147c964c8095723d0beb8914e3")]
    [InlineData(LoginAudioCue.ServerSelect, 22050, 1, 41472, "fdd34965d059bdc27667113e9d98a1a2dad9908c913f99ac0ff5b3ae28e2cf5e")]
    [InlineData(LoginAudioCue.Page, 22050, 1, 33408, "bfc6cdd3fab21ae4efbd3707e96c1188b17520463999c48921577c4a355e94b3")]
    [InlineData(LoginAudioCue.Dragon, 22050, 2, 112896, "d509867f9a57370d2a6837e31322287bd97e88a477e272c0fe23a7fbda2c59b9")]
    [InlineData(LoginAudioCue.ButtonClick, 22050, 1, 10368, "ebfd04942add2f4fd32cce06dc9224e1e7fdbccad7d66924321b79f74c6d9d5b")]
    [InlineData(LoginAudioCue.ButtonHover, 22050, 1, 9216, "b7d82e29dcf09de0768b4d3d78cfa9ca3e74b3e3315c0c41a477d43cc7a0a31b")]
    [InlineData(LoginAudioCue.Confirm, 22050, 2, 324864, "e09699da989573e077a9c0ae67d0aa3a70158ac16e439a1fc043b3a68274d04a")]
    [InlineData(LoginAudioCue.KeyOne, 22050, 1, 23040, "76e683239293f3b797ee338eab0cd028247f79081f6efe65b2bb00bec413a153")]
    [InlineData(LoginAudioCue.KeyTwo, 22050, 1, 31104, "b6186b1da76a320e38e0c6490e251deb9af1d1a97bf4752d1d22b02ad5b4a0bb")]
    [InlineData(LoginAudioCue.KeyThree, 22050, 1, 23040, "141bcc2fa5f83987b5423d259bcf637c9324a85880b78cad1354c9dfc1b79bae")]
    [InlineData(LoginAudioCue.Error, 22050, 1, 41472, "25b43d6597e9f320e6895a0363b5efcfb9a3911ddfc45600ac66875fc5b9e067")]
    public void RetailSoundMatchesSourceDerivedPcm(LoginAudioCue cue, int sampleRate, int channels, int expectedBytes, string sha256)
    {
        LoginAudioClip clip = LoginAudioAssets.Load(cue);
        Assert.Equal(sampleRate, clip.SampleRate);
        Assert.Equal(channels, clip.ChannelCount);
        Assert.Equal(expectedBytes, clip.Pcm16.Length);
        Assert.Equal(sha256, Convert.ToHexStringLower(SHA256.HashData(clip.Pcm16.Span)));
    }

    [Fact]
    public void UnknownSoundIdentityIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LoginAudioAssets.Load((LoginAudioCue)99));
    }
}
