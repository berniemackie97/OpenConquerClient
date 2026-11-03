using System.Buffers.Binary;
using OpenConquer.Client.World;
using OpenConquer.Content.Map;

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
        WorldScenerySprite sprite = Assert.Single(sprites.Where(s => ReferenceEquals(s.Source, record)));

        ReadOnlySpan<byte> encoded = record.EncodedPayload.Span;
        int offsetX = BinaryPrimitives.ReadInt32LittleEndian(encoded[0x194..]);
        int offsetY = BinaryPrimitives.ReadInt32LittleEndian(encoded[0x198..]);
        Assert.Equal((400 - offsetX, 300 - offsetY), (sprite.X, sprite.Y));
        Assert.Equal((record.TileX, record.TileY), (sprite.TileX, sprite.TileY));
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
        List<WorldScenerySprite> active = [];
        WorldSceneryDepthOrder.Insert(active, animated);
        WorldSceneryDepthOrder.Insert(active, inFront);
        WorldSceneryDepthOrder.Insert(active, behind);

        Assert.Equal([behind, animated, inFront], active);
    }

    [Fact]
    public void EqualDepthPreservesActivationOrder()
    {
        using WorldMapFixture fixture = new();
        GameMapSceneryAniBinding binding = fixture.Scenery[0];
        WorldScenerySprite earlier = new(binding.Animation, 0, 0, 0, 256, 256, 70, 70, 2, 2);
        WorldScenerySprite later = new(binding.Animation, 0, 1, 1, 256, 256, 70, 70, 2, 2);
        List<WorldScenerySprite> active = [];
        WorldSceneryDepthOrder.Insert(active, earlier);
        WorldSceneryDepthOrder.Insert(active, later);
        Assert.Equal([earlier, later], active);
    }
}
