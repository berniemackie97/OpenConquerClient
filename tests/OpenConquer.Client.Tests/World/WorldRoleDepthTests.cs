using System.Text;
using OpenConquer.Content.Ani;
using OpenConquer.Rendering.World;

namespace OpenConquer.Client.Tests.World;

public sealed class WorldRoleDepthTests
{
    [Fact]
    public void InsertsHeroBetweenSceneryObjectsByTileOrder()
    {
        WorldSceneryDepthOrder order = new();
        order.Insert(Sprite(80, 80));
        order.Insert(Sprite(70, 70));
        order.InsertRole(new WorldSceneRole(0, 75, 75, 0));

        IReadOnlyList<WorldScenerySprite> drawn = order.ToDrawListWithRoles(
            out IReadOnlyList<WorldRoleDrawSlot> roles);
        int heroIndex = Assert.Single(roles).SpriteIndex;

        Assert.Equal(1, heroIndex);
        Assert.Equal([(70, 70), (80, 80)], drawn.Select(s => (s.TileX, s.TileY)));
    }

    [Fact]
    public void TerrainFootprintAdoptsHeroAfterParent()
    {
        WorldSceneryDepthOrder order = new();
        order.Insert(Sprite(80, 80), terrain: true);
        order.InsertRole(new WorldSceneRole(0, 80, 80, 0));

        IReadOnlyList<WorldScenerySprite> drawn = order.ToDrawListWithRoles(
            out IReadOnlyList<WorldRoleDrawSlot> roles);
        int heroIndex = Assert.Single(roles).SpriteIndex;

        Assert.Equal(1, heroIndex);
        Assert.Single(drawn);
    }

    [Fact]
    public void ExclusiveFootprintBoundaryDoesNotAdoptHero()
    {
        WorldSceneryDepthOrder order = new();
        order.Insert(Sprite(80, 80), terrain: true);
        order.InsertRole(new WorldSceneRole(0, 79, 79, 0));

        _ = order.ToDrawListWithRoles(out IReadOnlyList<WorldRoleDrawSlot> roles);
        Assert.Equal(0, Assert.Single(roles).SpriteIndex);
    }

    private static WorldScenerySprite Sprite(int x, int y)
    {
        string raw = "[Scene]\nFrameAmount=1\nFrame0=data/scene/test.dds\n";
        using MemoryStream stream = new(Encoding.Latin1.GetBytes(raw));
        AniIndexSection animation = AniIndexFile.Load(stream, "ani/scene.ani").GetRequiredSection("Scene");
        return new WorldScenerySprite(animation, 0, 0, 0, 256, 256, x, y, 1, 1);
    }
}
