using OpenConquer.Launcher.Installation;

namespace OpenConquer.Launcher.Tests;

public sealed class ReleasePackagePathTests
{
    [Theory]
    [InlineData("client.dll")]
    [InlineData("native/libclient.so")]
    [InlineData("content/caf\u00e9.txt")]
    public void IsValidAcceptsPortablePaths(string path)
    {
        Assert.True(ReleasePackagePath.IsValid(path));
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("con.txt")]
    [InlineData("native/COM1.dll")]
    [InlineData("native/lpt\u00b2.log")]
    [InlineData("content/file:name")]
    [InlineData("content/file?.txt")]
    [InlineData("content/cafe\u0301.txt")]
    public void IsValidRejectsPathsThatAreNotPortableToWindows(string path)
    {
        Assert.False(ReleasePackagePath.IsValid(path));
    }

    [Fact]
    public void IsValidRejectsSegmentThatExceedsPortableUtf8Limit()
    {
        Assert.False(ReleasePackagePath.IsValid(new string('é', 128)));
    }

    [Fact]
    public void IsValidRejectsUnpairedSurrogate()
    {
        Assert.False(ReleasePackagePath.IsValid("content/" + new string('\ud800', 1) + ".txt"));
        Assert.False(ReleasePackagePath.IsValid("content/" + new string('\udc00', 1) + ".txt"));
    }
}
