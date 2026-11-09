using System.Text.Json;
using OpenConquer.Rendering.World;

namespace OpenConquer.Client.Tests.World;

public sealed class WorldOffMapNativeGoldenTests
{
    [Fact]
    public void ProjectionMatchesStaticSignedInstructionGoldenCases()
    {
        using WorldMapFixture map = new();
        using JsonDocument fixture = Fixture();
        Assert.Equal(fixture.RootElement.GetProperty("mapWidth").GetInt32(), map.Map.CellGrid.Width);
        Assert.Equal(fixture.RootElement.GetProperty("mapHeight").GetInt32(), map.Map.CellGrid.Height);
        foreach (JsonElement row in fixture.RootElement.GetProperty("projection").EnumerateArray())
        {
            (int x, int y) = WorldCameraProjection.ProjectTile(map.Map, Int(row, "tileX"), Int(row, "tileY"));
            Assert.True((x, y) == (Int(row, "worldX"), Int(row, "worldY")), row.ToString());
        }
    }

    [Fact]
    public void VisibilityMatchesStaticInclusiveScreenBoundsGoldenCases()
    {
        using WorldMapFixture map = new();
        using JsonDocument fixture = Fixture();
        foreach (JsonElement row in fixture.RootElement.GetProperty("visibility").EnumerateArray())
        {
            WorldPuzzleProjection projection = new(800, 600, 0, 0,
                Int(row, "centerX"), Int(row, "centerY"), Int(row, "scale"), 256);
            bool visible = WorldCameraProjection.IsTileWithinScreenBounds(map.Map, projection,
                Int(row, "tileX"), Int(row, "tileY"), margin: 2);
            Assert.True(visible == row.GetProperty("visible").GetBoolean(), row.ToString());
        }
    }

    [Fact]
    public void DepthMatchesStaticOffGridAndWrappingBranchGoldenCases()
    {
        using WorldMapFixture map = new();
        using JsonDocument fixture = Fixture();
        foreach (JsonElement row in fixture.RootElement.GetProperty("depth").EnumerateArray())
        {
            WorldSceneryDepthOrder order = new();
            foreach (JsonElement sprite in row.GetProperty("sprites").EnumerateArray())
            {
                order.Insert(new WorldScenerySprite(map.Scenery[0].Animation, 0, 0, 0, 256, 256,
                    Int(sprite, "x"), Int(sprite, "y"), Int(sprite, "width"), Int(sprite, "height")),
                    sprite.GetProperty("terrain").GetBoolean());
            }

            order.InsertRole(new WorldSceneRole(0, Int(row, "x"), Int(row, "y"), 0));
            _ = order.ToDrawListWithRoles(out IReadOnlyList<WorldRoleDrawSlot> roles);
            Assert.True(Assert.Single(roles).SpriteIndex == Int(row, "index"),
                row.GetProperty("name").GetString());
        }
    }

    [Theory]
    [InlineData(-1, 60, true)]
    [InlineData(132, 132, false)]
    public void RoleInsertionUsesScreenVisibilityInsteadOfMapCellBounds(int tileX, int tileY, bool visible)
    {
        using WorldMapFixture map = new();
        WorldPuzzleProjection projection = new(800, 600, 0, 0, 2304, 960, 256, 256);
        IReadOnlyList<WorldScenerySprite> sprites = WorldSceneryDrawList.BuildWithRoles(map.Map, [],
            new WorldSceneryIndex(map.Map, map.Scenery),
            new Dictionary<OpenConquer.Content.Map.GameMapSceneryRecord, (int Width, int Height)>(),
            projection, 1000, 1000, new WorldSceneRole(0, tileX, tileY, 0), [],
            out IReadOnlyList<WorldRoleDrawSlot> roles);

        if (visible)
        {
            Assert.InRange(Assert.Single(roles).SpriteIndex, 0, sprites.Count);
        }
        else
        {
            Assert.Empty(roles);
        }
    }

    private static int Int(JsonElement row, string name) => row.GetProperty(name).GetInt32();

    private static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllBytes(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "World", "off-map-5517.json")));
}
