namespace OpenConquer.Rendering.Tests;

public sealed class SpriteSourceRectangleTests
{
    [Fact]
    public void Constructor_ExposesCoordinatesAndDimensions()
    {
        SpriteSourceRectangle rectangle = new(x: 3, y: 5, width: 7, height: 11);

        Assert.Equal(3, rectangle.X);
        Assert.Equal(5, rectangle.Y);
        Assert.Equal(7, rectangle.Width);
        Assert.Equal(11, rectangle.Height);
        Assert.Equal(10, rectangle.Right);
        Assert.Equal(16, rectangle.Bottom);
    }

    [Fact]
    public void Constructor_AcceptsTheTopLeftOriginAndSmallestPositiveDimensions()
    {
        SpriteSourceRectangle rectangle = new(x: 0, y: 0, width: 1, height: 1);

        Assert.Equal(0, rectangle.X);
        Assert.Equal(0, rectangle.Y);
        Assert.Equal(1, rectangle.Width);
        Assert.Equal(1, rectangle.Height);
        Assert.Equal(1, rectangle.Right);
        Assert.Equal(1, rectangle.Bottom);
    }

    [Fact]
    public void Constructor_AcceptsMaximumComponentValuesWithoutOverflowingDerivedEdges()
    {
        SpriteSourceRectangle rectangle = new(
            x: int.MaxValue,
            y: int.MaxValue,
            width: int.MaxValue,
            height: int.MaxValue);

        Assert.Equal(4_294_967_294L, rectangle.Right);
        Assert.Equal(4_294_967_294L, rectangle.Bottom);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Constructor_ThrowsArgumentOutOfRangeExceptionWhenXIsNegative(int x)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new SpriteSourceRectangle(x, y: 0, width: 1, height: 1));

        Assert.Equal("x", exception.ParamName);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Constructor_ThrowsArgumentOutOfRangeExceptionWhenYIsNegative(int y)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new SpriteSourceRectangle(x: 0, y, width: 1, height: 1));

        Assert.Equal("y", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Constructor_ThrowsArgumentOutOfRangeExceptionWhenWidthIsNotPositive(int width)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new SpriteSourceRectangle(x: 0, y: 0, width, height: 1));

        Assert.Equal("width", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Constructor_ThrowsArgumentOutOfRangeExceptionWhenHeightIsNotPositive(int height)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new SpriteSourceRectangle(x: 0, y: 0, width: 1, height));

        Assert.Equal("height", exception.ParamName);
    }

    [Fact]
    public void Constructor_ValidatesXBeforeYAndDimensions()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new SpriteSourceRectangle(x: -1, y: -1, width: 0, height: 0));

        Assert.Equal("x", exception.ParamName);
    }

    [Fact]
    public void Constructor_ValidatesYBeforeDimensions()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new SpriteSourceRectangle(x: 0, y: -1, width: 0, height: 0));

        Assert.Equal("y", exception.ParamName);
    }

    [Fact]
    public void Constructor_ValidatesWidthBeforeHeight()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new SpriteSourceRectangle(x: 0, y: 0, width: 0, height: 0));

        Assert.Equal("width", exception.ParamName);
    }

    [Fact]
    public void Equality_TreatsMatchingRectanglesAsEqual()
    {
        SpriteSourceRectangle left = new(x: 2, y: 3, width: 4, height: 5);
        SpriteSourceRectangle right = new(x: 2, y: 3, width: 4, height: 5);

        Assert.Equal(left, right);
        Assert.True(left == right);
        Assert.False(left != right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Equality_DistinguishesDifferentRectangleComponents()
    {
        SpriteSourceRectangle rectangle = new(x: 2, y: 3, width: 4, height: 5);

        Assert.NotEqual(rectangle, new SpriteSourceRectangle(x: 1, y: 3, width: 4, height: 5));
        Assert.NotEqual(rectangle, new SpriteSourceRectangle(x: 2, y: 1, width: 4, height: 5));
        Assert.NotEqual(rectangle, new SpriteSourceRectangle(x: 2, y: 3, width: 1, height: 5));
        Assert.NotEqual(rectangle, new SpriteSourceRectangle(x: 2, y: 3, width: 4, height: 1));
    }

    [Fact]
    public void DefaultInstance_HasZeroComponentsAndBypassesValidation()
    {
        SpriteSourceRectangle rectangle = default;

        Assert.Equal(0, rectangle.X);
        Assert.Equal(0, rectangle.Y);
        Assert.Equal(0, rectangle.Width);
        Assert.Equal(0, rectangle.Height);
        Assert.Equal(0, rectangle.Right);
        Assert.Equal(0, rectangle.Bottom);
    }
}
