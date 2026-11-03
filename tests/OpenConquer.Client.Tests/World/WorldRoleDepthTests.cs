using System.Text;
using OpenConquer.Content.Ani;
using OpenConquerStudio.ConquerRendering;

namespace OpenConquer.Client.Tests.World;

public sealed class WorldRoleDepthTests
{
    [Fact]
    public void InsertsHeroBetweenSceneryObjectsByTileOrder()
    {
        WorldSceneryDepthOrder order = new();
        order.Insert(Sprite(80, 80));
        order.Insert(Sprite(70, 70));
        order.InsertRole(75, 75);

        IReadOnlyList<WorldScenerySprite> drawn = order.ToDrawList(out int heroIndex);

        Assert.Equal(1, heroIndex);
        Assert.Equal([(70, 70), (80, 80)], drawn.Select(s => (s.TileX, s.TileY)));
    }

    [Fact]
    public void TerrainFootprintAdoptsHeroAfterParent()
    {
        WorldSceneryDepthOrder order = new();
        order.Insert(Sprite(80, 80), terrain: true);
        order.InsertRole(79, 80);

        IReadOnlyList<WorldScenerySprite> drawn = order.ToDrawList(out int heroIndex);

        Assert.Equal(1, heroIndex);
        Assert.Single(drawn);
    }

    [Fact]
    public void ExclusiveFootprintBoundaryDoesNotAdoptHero()
    {
        WorldSceneryDepthOrder order = new();
        order.Insert(Sprite(80, 80), terrain: true);
        order.InsertRole(79, 79);

        _ = order.ToDrawList(out int heroIndex);
        Assert.Equal(0, heroIndex);
    }

    private static WorldScenerySprite Sprite(int x, int y)
    {
        string raw = "[Scene]\nFrameAmount=1\nFrame0=data/scene/test.dds\n";
        using MemoryStream stream = new(Encoding.Latin1.GetBytes(raw));
        AniIndexSection animation = AniIndexFile.Load(stream, "ani/scene.ani").GetRequiredSection("Scene");
        return new WorldScenerySprite(animation, 0, 0, 0, 256, 256, x, y, 1, 1);
    }
}
