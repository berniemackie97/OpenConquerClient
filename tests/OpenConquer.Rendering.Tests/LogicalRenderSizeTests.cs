namespace OpenConquer.Rendering.Tests;

public sealed class LogicalRenderSizeTests
{
    [Fact]
    public void Constructor_ExposesWidthAndHeight()
    {
        LogicalRenderSize size = new(width: 800, height: 600);

        Assert.Equal(800, size.Width);
        Assert.Equal(600, size.Height);
    }

    [Fact]
    public void Constructor_AcceptsTheSmallestPositiveDimensions()
    {
        LogicalRenderSize size = new(width: 1, height: 1);

        Assert.Equal(1, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Constructor_AcceptsMaximumDimensions()
    {
        LogicalRenderSize size = new(width: int.MaxValue, height: int.MaxValue);

        Assert.Equal(int.MaxValue, size.Width);
        Assert.Equal(int.MaxValue, size.Height);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Constructor_ThrowsArgumentOutOfRangeExceptionWhenWidthIsNotPositive(int width)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new LogicalRenderSize(width, height: 600));

        // The parameter name is what tells a caller which dimension it got wrong, so it is
        // asserted rather than only the exception type.
        Assert.Equal("width", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Constructor_ThrowsArgumentOutOfRangeExceptionWhenHeightIsNotPositive(int height)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new LogicalRenderSize(width: 800, height));

        Assert.Equal("height", exception.ParamName);
    }

    [Fact]
    public void Constructor_ValidatesWidthBeforeHeight()
    {
        // Both dimensions are invalid, so the reported parameter proves the validation order
        // rather than which check happens to run first by accident.
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new LogicalRenderSize(width: 0, height: 0));

        Assert.Equal("width", exception.ParamName);
    }

    [Fact]
    public void Equality_TreatsMatchingDimensionsAsEqual()
    {
        LogicalRenderSize left = new(width: 1024, height: 768);
        LogicalRenderSize right = new(width: 1024, height: 768);

        Assert.Equal(left, right);
        Assert.True(left == right);
        Assert.False(left != right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Equality_DistinguishesWidthFromHeight()
    {
        // A square-agnostic comparison would call these equal; a transposed size is a
        // different render target.
        LogicalRenderSize wide = new(width: 1024, height: 768);
        LogicalRenderSize tall = new(width: 768, height: 1024);

        Assert.NotEqual(wide, tall);
        Assert.True(wide != tall);
    }

    [Fact]
    public void DefaultInstance_HasZeroDimensionsAndBypassesValidation()
    {
        // A record struct always has a parameterless default that the constructor cannot
        // guard. Callers must not treat default(LogicalRenderSize) as a usable size, and
        // this test records that the type does not pretend otherwise.
        LogicalRenderSize size = default;

        Assert.Equal(0, size.Width);
        Assert.Equal(0, size.Height);
    }
}
