using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Rendering.Tests;

public sealed class SpriteSourceBoundsTests
{
    [Fact]
    public void Constructor_ExposesPixelEdges()
    {
        SpriteSourceBounds bounds = new(left: 3, top: 5, right: 11, bottom: 17);

        Assert.Equal(3, bounds.Left);
        Assert.Equal(5, bounds.Top);
        Assert.Equal(11, bounds.Right);
        Assert.Equal(17, bounds.Bottom);
    }

    [Fact]
    public void Constructor_AcceptsDegenerateBounds()
    {
        SpriteSourceBounds bounds = new(left: 4, top: 7, right: 4, bottom: 7);

        Assert.Equal(4, bounds.Left);
        Assert.Equal(7, bounds.Top);
        Assert.Equal(4, bounds.Right);
        Assert.Equal(7, bounds.Bottom);
    }

    [Fact]
    public void Constructor_AcceptsBoundsBeyondATextureExtent()
    {
        SpriteSourceBounds bounds = new(left: 0, top: 18, right: 8, bottom: 35);

        Assert.Equal(0, bounds.Left);
        Assert.Equal(18, bounds.Top);
        Assert.Equal(8, bounds.Right);
        Assert.Equal(35, bounds.Bottom);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Constructor_RejectsNegativeLeftEdge(int left)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => new SpriteSourceBounds(left, top: 0, right: 0, bottom: 0));

        Assert.Equal("left", exception.ParamName);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Constructor_RejectsNegativeTopEdge(int top)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => new SpriteSourceBounds(left: 0, top, right: 0, bottom: 0));

        Assert.Equal("top", exception.ParamName);
    }

    [Fact]
    public void Constructor_RejectsRightEdgeBeforeLeftEdge()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => new SpriteSourceBounds(left: 4, top: 0, right: 3, bottom: 0));

        Assert.Equal("right", exception.ParamName);
    }

    [Fact]
    public void Constructor_RejectsBottomEdgeBeforeTopEdge()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => new SpriteSourceBounds(left: 0, top: 4, right: 0, bottom: 3));

        Assert.Equal("bottom", exception.ParamName);
    }

    [Fact]
    public void Equality_TreatsMatchingBoundsAsEqual()
    {
        SpriteSourceBounds left = new(left: 1, top: 2, right: 3, bottom: 4);
        SpriteSourceBounds right = new(left: 1, top: 2, right: 3, bottom: 4);

        Assert.Equal(left, right);
        Assert.True(left == right);
        Assert.False(left != right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Equality_DistinguishesDifferentEdges()
    {
        SpriteSourceBounds bounds = new(left: 1, top: 2, right: 3, bottom: 4);

        Assert.NotEqual(bounds, new SpriteSourceBounds(left: 0, top: 2, right: 3, bottom: 4));
        Assert.NotEqual(bounds, new SpriteSourceBounds(left: 1, top: 0, right: 3, bottom: 4));
        Assert.NotEqual(bounds, new SpriteSourceBounds(left: 1, top: 2, right: 4, bottom: 4));
        Assert.NotEqual(bounds, new SpriteSourceBounds(left: 1, top: 2, right: 3, bottom: 5));
    }

    [Fact]
    public void DefaultInstance_IsTheDegenerateOrigin()
    {
        SpriteSourceBounds bounds = default;

        Assert.Equal(0, bounds.Left);
        Assert.Equal(0, bounds.Top);
        Assert.Equal(0, bounds.Right);
        Assert.Equal(0, bounds.Bottom);
    }
}
