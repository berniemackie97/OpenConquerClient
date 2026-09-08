namespace OpenConquer.Product.Tool.Tests;

public sealed class ProductReleasePathTests
{
    [Theory]
    [InlineData("client.dll")]
    [InlineData("native/libclient.so")]
    [InlineData("content/caf\u00e9.txt")]
    public void IsValidAcceptsPortablePaths(string path)
    {
        Assert.True(ProductReleasePath.IsValid(path));
    }

    [Theory]
    [InlineData("AUX")]
    [InlineData("nul.json")]
    [InlineData("native/COM9.dll")]
    [InlineData("native/lpt\u00b3.log")]
    [InlineData("content/file|name")]
    [InlineData("content/file*.txt")]
    [InlineData("content/cafe\u0301.txt")]
    public void IsValidRejectsPathsThatAreNotPortableToWindows(string path)
    {
        Assert.False(ProductReleasePath.IsValid(path));
    }

    [Fact]
    public void IsValidRejectsSegmentThatExceedsPortableUtf8Limit()
    {
        Assert.False(ProductReleasePath.IsValid(new string('é', 128)));
    }

    [Fact]
    public void IsValidRejectsUnpairedSurrogate()
    {
        Assert.False(ProductReleasePath.IsValid("content/" + new string('\ud800', 1) + ".txt"));
        Assert.False(ProductReleasePath.IsValid("content/" + new string('\udc00', 1) + ".txt"));
    }
}
