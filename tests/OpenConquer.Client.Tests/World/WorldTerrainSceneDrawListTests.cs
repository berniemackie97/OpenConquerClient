using System.Text;
using OpenConquer.Client.World;
using OpenConquer.Content.Ani;
using OpenConquer.Content.Map;

namespace OpenConquer.Client.Tests.World;

public sealed class WorldTerrainSceneDrawListTests
{
    [Fact]
    public void SceneObject_UsesProjectedAnchorOffsetAndElapsedAnimationFrame()
    {
        using WorldMapFixture fixture = new();
        AniIndexSection animation = Animation(3);
        GameMapTerrainScenePlacement placement = Placement(animation, 60, 108, -60, -48, 200);

        IReadOnlyList<WorldTerrainSprite> sprites = WorldTerrainSceneDrawList.Build(
            fixture.Map, [placement],
            new WorldPuzzleProjection(800, 600, 0, 0, 2500, 2600, 256, 256),
            nowMilliseconds: 1400, sceneStartMilliseconds: 1000);

        WorldTerrainSprite sprite = Assert.Single(sprites);
        Assert.Same(animation, sprite.Animation);
        Assert.Equal(2, sprite.FrameIndex);
        Assert.Equal((128, 56), (sprite.X, sprite.Y));
        Assert.Equal((60, 108), (sprite.TileX, sprite.TileY));
    }

    [Fact]
    public void SceneObject_ScalesAroundViewportCenterAndWrapsAnimation()
    {
        using WorldMapFixture fixture = new();
        GameMapTerrainScenePlacement placement = Placement(Animation(3), 60, 108, -60, -48, 200);
        WorldPuzzleProjection projection = new(800, 600, 0, 0, 2500, 2600, 384, 256);

        WorldTerrainSprite sprite = Assert.Single(WorldTerrainSceneDrawList.Build(
            fixture.Map, [placement], projection, nowMilliseconds: 1600, sceneStartMilliseconds: 1000));

        Assert.Equal((-8, -66), (sprite.X, sprite.Y));
        Assert.Equal(0, sprite.FrameIndex);
        Assert.Equal((384, 256), (sprite.ScaleNumerator, sprite.ScaleDenominator));
    }

    [Fact]
    public void SceneObjects_UseViewportMarginAndDepthOrderRatherThanFileOrder()
    {
        using WorldMapFixture fixture = new();
        AniIndexSection animation = Animation(1);
        WorldPuzzleProjection projection = new(5000, 5000, 0, 0, 0, 0, 256, 256);
        GameMapTerrainScenePlacement[] placements =
        [
            Placement(animation, 71, 70, 0, 0, 100),
            Placement(animation, 70, 70, 0, 0, 100),
            Placement(animation, 69, 69, 0, 0, 100),
        ];

        IReadOnlyList<WorldTerrainSprite> sprites = WorldTerrainSceneDrawList.Build(
            fixture.Map, placements, projection, nowMilliseconds: 1000, sceneStartMilliseconds: 1000);

        Assert.Equal([(69, 69), (70, 70), (71, 70), (10, 10)],
            sprites.Select(static sprite => (sprite.TileX, sprite.TileY)));
    }

    [Fact]
    public void OverlappingTerrainFootprints_UseTileAxisOrdering()
    {
        using WorldMapFixture fixture = new();
        AniIndexSection animation = Animation(1);
        WorldPuzzleProjection projection = new(5000, 5000, 0, 0, 0, 0, 256, 256);
        GameMapTerrainScenePlacement[] placements =
        [
            Placement(animation, 70, 70, 0, 0, 100, width: 2),
            Placement(animation, 71, 69, 0, 0, 100, width: 2),
        ];

        IReadOnlyList<WorldTerrainSprite> sprites = WorldTerrainSceneDrawList.Build(
            fixture.Map, placements, projection, 1000, 1000);

        Assert.Equal([(71, 69), (70, 70)],
            sprites.Select(static sprite => (sprite.TileX, sprite.TileY)));
    }

    [Fact]
    public void SceneObject_OutsideVisibilityMargin_IsNotDrawn()
    {
        using WorldMapFixture fixture = new();
        WorldPuzzleProjection projection = new(800, 600, 0, 0, 2500, 2600, 256, 256);

        IReadOnlyList<WorldTerrainSprite> sprites = WorldTerrainSceneDrawList.Build(
            fixture.Map, [Placement(Animation(1), 10, 10, 0, 0, 100)],
            projection, 1000, 1000);

        Assert.Empty(sprites);
    }

    private static GameMapTerrainScenePlacement Placement(AniIndexSection animation, int x, int y,
        int offsetX, int offsetY, int frameInterval, int width = 1)
    {
        GameMapTerrainObject terrain = new("ani/MapScene.ani", animation.Name, offsetX, offsetY,
            frameInterval, width, 1, 0, 0, 0, 0, []);
        GameMapSceneryRecord scenery = new(1, "map/Scene/test.scene", null, x, y, ReadOnlyMemory<byte>.Empty);
        return new GameMapTerrainScenePlacement(scenery, [new GameMapTerrainObjectBinding(terrain, animation)]);
    }

    private static AniIndexSection Animation(int frameCount)
    {
        StringBuilder index = new("[Test]\nFrameAmount=").Append(frameCount).Append('\n');
        for (int i = 0; i < frameCount; i++)
        {
            index.Append("Frame").Append(i).Append("=data/map/test").Append(i).Append(".dds\n");
        }

        using MemoryStream stream = new(Encoding.Latin1.GetBytes(index.ToString()));
        return AniIndexFile.Load(stream, "ani/test.ani").GetRequiredSection("Test");
    }
}
