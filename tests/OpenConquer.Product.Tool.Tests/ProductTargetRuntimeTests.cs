namespace OpenConquer.Product.Tool.Tests;

public sealed class ProductTargetRuntimeTests
{
    [Theory]
    [InlineData("win-x64")]
    [InlineData("win-arm64")]
    [InlineData("osx-x64")]
    [InlineData("osx-arm64")]
    [InlineData("linux-x64")]
    [InlineData("linux-arm64")]
    public void IsSupportedAcceptsProductRuntime(string targetRuntime)
    {
        Assert.True(ProductTargetRuntime.IsSupported(targetRuntime));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("win-x86")]
    [InlineData("osx")]
    [InlineData("linux-riscv64")]
    [InlineData("OSX-ARM64")]
    public void IsSupportedRejectsUnsupportedRuntime(string? targetRuntime)
    {
        Assert.False(ProductTargetRuntime.IsSupported(targetRuntime));
    }

    [Fact]
    public void CurrentReturnsSupportedRuntimeOnSupportedHost()
    {
        string? current = ProductTargetRuntime.Current;

        if (current is null)
        {
            return;
        }

        Assert.True(ProductTargetRuntime.IsSupported(current));
    }
}
