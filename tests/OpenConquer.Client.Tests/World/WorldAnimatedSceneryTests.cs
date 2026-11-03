using System.Buffers.Binary;
using OpenConquer.Content.Ani;
using OpenConquer.Content.Map;
using OpenConquer.Rendering.World;

namespace OpenConquer.Client.Tests.World;

public sealed class WorldAnimatedSceneryTests
{
    [Fact]
    public void NewbieVillage_BindsAllConfiguredAnimatedScenery()
    {
        using WorldMapFixture fixture = new();
        Assert.Equal(18, fixture.Scenery.Count);
        Assert.Equal(16, fixture.Scenery.Select(binding => binding.Animation.Name).Distinct().Count());
    }

    [Fact]
    public void AnimationUsesStoredTileOffsetDepthSpanAndInterval()
    {
        using WorldMapFixture fixture = new();
        GameMapSceneryAniBinding binding = fixture.Scenery[0];
        GameMapSceneryRecord record = binding.Scenery;
        (int worldX, int worldY) = WorldCameraProjection.ProjectTile(
            fixture.Map, record.TileX!.Value, record.TileY!.Value);
        WorldPuzzleProjection projection = new(800, 600, 0, 0, worldX - 400, worldY - 300, 256, 256);
        WorldSceneryIndex index = new(fixture.Map, fixture.Scenery);

        IReadOnlyList<WorldScenerySprite> sprites = WorldSceneryDrawList.Build(
            fixture.Map, [], index,
            new Dictionary<GameMapSceneryRecord, (int Width, int Height)>(),
            projection, 1000, 1000);
        WorldScenerySprite sprite = Assert.Single(sprites, s => ReferenceEquals(s.Source, record));

        ReadOnlySpan<byte> encoded = record.EncodedPayload.Span;
        int offsetX = BinaryPrimitives.ReadInt32LittleEndian(encoded[0x194..]);
        int offsetY = BinaryPrimitives.ReadInt32LittleEndian(encoded[0x198..]);
        Assert.Equal((400 - offsetX, 300 - offsetY), (sprite.X, sprite.Y));
        Assert.Equal((record.TileX!.Value, record.TileY!.Value), (sprite.TileX, sprite.TileY));
        Assert.Equal(BinaryPrimitives.ReadInt32LittleEndian(encoded[0x18C..]), sprite.FootprintWidth);
        Assert.Equal(BinaryPrimitives.ReadInt32LittleEndian(encoded[0x190..]), sprite.FootprintHeight);
        Assert.Equal(0, sprite.FrameIndex);
    }

    [Fact]
    public void AnimatedSceneryIsNotTickedWhenItsSpatialBucketIsOutsideTheViewport()
    {
        using WorldMapFixture fixture = new();
        GameMapSceneryAniBinding first = fixture.Scenery[0];
        (int x, int y) = WorldCameraProjection.ProjectTile(
            fixture.Map, first.Scenery.TileX!.Value, first.Scenery.TileY!.Value);
        WorldSceneryIndex index = new(fixture.Map, fixture.Scenery);

        Assert.Contains(first, index.Collect(new WorldPuzzleProjection(
            800, 600, 0, 0, x - 400, y - 300, 256, 256)));
        Assert.DoesNotContain(first, index.Collect(new WorldPuzzleProjection(
            800, 600, 0, 0, x + 8000, y + 8000, 256, 256)));
    }

    [Fact]
    public void AnimatedSceneryAndTerrainShareTheSameDepthOrdering()
    {
        using WorldMapFixture fixture = new();
        GameMapSceneryAniBinding binding = fixture.Scenery[0];
        (int x, int y) = (binding.Scenery.TileX!.Value, binding.Scenery.TileY!.Value);
        WorldScenerySprite behind = new(binding.Animation, 0, 0, 0, 256, 256, x - 1, y - 1, 1, 1);
        WorldScenerySprite animated = new(binding.Animation, 0, 0, 0, 256, 256, x, y, 2, 2, binding.Scenery);
        WorldScenerySprite inFront = new(binding.Animation, 0, 0, 0, 256, 256, x + 1, y + 1, 1, 1);
        WorldSceneryDepthOrder order = new();
        order.Insert(animated);
        order.Insert(inFront);
        order.Insert(behind);

        Assert.Equal([behind, animated, inFront], order.ToDrawList());
    }

    [Fact]
    public void EqualDepthPreservesActivationOrder()
    {
        using WorldMapFixture fixture = new();
        GameMapSceneryAniBinding binding = fixture.Scenery[0];
        WorldScenerySprite earlier = new(binding.Animation, 0, 0, 0, 256, 256, 70, 70, 2, 2);
        WorldScenerySprite later = new(binding.Animation, 0, 1, 1, 256, 256, 70, 70, 2, 2);
        WorldSceneryDepthOrder order = new();
        order.Insert(earlier);
        order.Insert(later);
        Assert.Equal([earlier, later], order.ToDrawList());
    }

    [Fact]
    public void TerrainFootprintCapturesLaterSpriteWithoutReorderingPeers()
    {
        using WorldMapFixture fixture = new();
        AniIndexSection animation = fixture.Scenery[0].Animation;
        WorldScenerySprite terrain = new(animation, 0, 0, 0, 256, 256, 70, 70, 4, 4);
        WorldScenerySprite peer = new(animation, 0, 0, 0, 256, 256, 71, 71, 1, 1);
        WorldScenerySprite child = new(animation, 0, 0, 0, 256, 256, 67, 70, 1, 1);
        WorldSceneryDepthOrder order = new();

        order.Insert(terrain, terrain: true);
        order.Insert(peer);
        order.Insert(child);

        Assert.Equal([terrain, child, peer], order.ToDrawList());
    }

    [Fact]
    public void AdoptedChildrenUseTileAxisOrdering()
    {
        using WorldMapFixture fixture = new();
        AniIndexSection animation = fixture.Scenery[0].Animation;
        WorldScenerySprite terrain = new(animation, 0, 0, 0, 256, 256, 70, 70, 4, 4);
        WorldScenerySprite right = new(animation, 0, 0, 0, 256, 256, 70, 70, 1, 1);
        WorldScenerySprite left = new(animation, 0, 0, 0, 256, 256, 68, 70, 1, 1);
        WorldScenerySprite middle = new(animation, 0, 0, 0, 256, 256, 69, 70, 1, 1);
        WorldSceneryDepthOrder order = new();

        order.Insert(terrain, terrain: true);
        order.Insert(right);
        order.Insert(middle);
        order.Insert(left);

        Assert.Equal([terrain, left, middle, right], order.ToDrawList());
    }

    [Fact]
    public void TerrainFootprintExcludesItsLowerTileBoundary()
    {
        using WorldMapFixture fixture = new();
        AniIndexSection animation = fixture.Scenery[0].Animation;
        WorldScenerySprite terrain = new(animation, 0, 0, 0, 256, 256, 70, 70, 4, 4);
        WorldScenerySprite outside = new(animation, 0, 0, 0, 256, 256, 66, 70, 1, 1);
        WorldScenerySprite inside = new(animation, 0, 0, 0, 256, 256, 67, 70, 1, 1);
        WorldSceneryDepthOrder order = new();

        order.Insert(terrain, terrain: true);
        order.Insert(outside);
        order.Insert(inside);

        Assert.Equal([outside, terrain, inside], order.ToDrawList());
    }

    [Fact]
    public void TerrainDoesNotRetroactivelyAdoptAnAlreadyActiveSprite()
    {
        using WorldMapFixture fixture = new();
        AniIndexSection animation = fixture.Scenery[0].Animation;
        WorldScenerySprite child = new(animation, 0, 0, 0, 256, 256, 67, 70, 1, 1);
        WorldScenerySprite terrain = new(animation, 0, 0, 0, 256, 256, 70, 70, 4, 4);
        WorldSceneryDepthOrder order = new();

        order.Insert(child);
        order.Insert(terrain, terrain: true);

        Assert.Equal([child, terrain], order.ToDrawList());
    }

    [Fact]
    public void AdoptedChildrenPreserveEqualDepthActivationOrder()
    {
        using WorldMapFixture fixture = new();
        AniIndexSection animation = fixture.Scenery[0].Animation;
        WorldScenerySprite terrain = new(animation, 0, 0, 0, 256, 256, 70, 70, 4, 4);
        WorldScenerySprite first = new(animation, 0, 0, 0, 256, 256, 69, 69, 1, 1);
        WorldScenerySprite second = new(animation, 0, 1, 1, 256, 256, 69, 69, 1, 1);
        WorldSceneryDepthOrder order = new();

        order.Insert(terrain, terrain: true);
        order.Insert(first);
        order.Insert(second);

        Assert.Equal([terrain, first, second], order.ToDrawList());
    }
}
