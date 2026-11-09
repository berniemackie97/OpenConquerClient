using System.Numerics;
using OpenConquer.Rendering.World;

namespace OpenConquer.Client.Tests.World;

public sealed class WorldMapSceneTests
{
    [Fact]
    public void LoadRejectsCancellationBeforeOpeningMapContent()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            WorldMapScene.Load(null!, null!, 1010, 60, 108, 0, cancellation.Token));
    }

    [Fact]
    public void NewbieVillage_FirstFrameOrdersBackdropGroundAndTerrain()
    {
        using WorldMapFixture fixture = new();
        WorldMapScene scene = new(fixture.Map, fixture.Puzzles, [], fixture.Scenery, 60, 108, 100, 1000);

        IReadOnlyList<WorldMapLayer> layers = scene.BuildFrame(800, 600, 1000);

        Assert.Equal(6, layers.Count);
        Assert.NotEmpty(layers[0].Puzzle!);
        Assert.NotEmpty(layers[1].Puzzle!);
        Assert.Null(layers[2].Puzzle);
        Assert.Null(layers[3].Puzzle);
        Assert.NotNull(layers[4].Scenery);
        Assert.Null(layers[5].Puzzle);

        (int x, int y) = WorldCameraProjection.ProjectTile(fixture.Map, 60, 108);
        WorldPuzzleProjection projection = WorldCameraProjection.FromHeroWorldPosition(
            fixture.Map, fixture.Puzzles, x, y, 800, 600);
        IReadOnlyList<WorldPuzzleTile> expected = WorldPuzzleDrawList.Ground(
            fixture.Map, fixture.Puzzles, projection, 0);

        Assert.Equal(expected, layers[1].Puzzle);
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    [InlineData((byte)3)]
    [InlineData((byte)4)]
    [InlineData((byte)5)]
    [InlineData((byte)6)]
    [InlineData((byte)7)]
    public void HeroTransformUsesTheCommittedEightWayFacing(byte direction)
    {
        using WorldMapFixture fixture = new();
        WorldMapScene scene = new(fixture.Map, fixture.Puzzles, [], fixture.Scenery, 60, 108, 100, 1000);
        scene.BuildFrame(800, 600, 1000);
        scene.SetHeroDirection(direction);

        (int x, int y) = WorldCameraProjection.ProjectTile(fixture.Map, 60, 108);
        WorldPuzzleProjection projection = WorldCameraProjection.FromHeroWorldPosition(
            fixture.Map, fixture.Puzzles, x, y, 800, 600);
        (Matrix4x4 Main, Matrix4x4 Shadow) expected = WorldRoleCameraMatrices.Create(
            fixture.Map, fixture.Puzzles, projection, x, y, (-1 - direction) * 45f, 0.75f);

        Assert.Equal(expected, scene.HeroMatrices);
    }

    [Fact]
    public void MovingPeerRebuildsTheCommittedDepthOrderWithNewTileAndFacing()
    {
        using WorldMapFixture fixture = new();
        WorldMapScene scene = new(fixture.Map, fixture.Puzzles, [], fixture.Scenery, 60, 108, 100, 1000);
        WorldSceneRole initial = new(1_000_001, 61, 108, 3);
        scene.SetRemoteRoles(1, [initial]);
        scene.BuildFrame(800, 600, 1000);

        WorldSceneRole first = Assert.Single(scene.CommittedRoleOrder,
            static slot => slot.Role.EntityId == 1_000_001).Role;
        Assert.Equal(initial, first);
        WorldSceneRole moved = new(1_000_001, 62, 108, 6);
        scene.SetRemoteRoles(2, [moved]);
        scene.BuildFrame(800, 600, 1034);

        WorldSceneRole current = Assert.Single(scene.CommittedRoleOrder,
            static slot => slot.Role.EntityId == 1_000_001).Role;
        Assert.Equal(moved, current);
        Assert.NotEqual(scene.RoleMatrices(first), scene.RoleMatrices(current));
        Assert.Equal(1, scene.CommittedRoleOrder.Count(static slot => slot.Role.EntityId == 1_000_001));
    }

    [Fact]
    public void PeerMarkersUseTheCommittedSceneFrameAndItsCamera()
    {
        using WorldMapFixture fixture = new();
        WorldMapScene scene = new(fixture.Map, fixture.Puzzles, [], fixture.Scenery, 60, 108, 100, 1000);
        WorldSceneRole nearby = new(1_000_001, 61, 108, 5);
        WorldSceneRole farther = new(1_000_002, 62, 109, 2);
        scene.SetRemoteRoles(1, [nearby, farther, new WorldSceneRole(1_000_003, 200, 200, 3)]);
        scene.BuildFrame(800, 600, 1000);

        Assert.Equal([0u, nearby.EntityId, farther.EntityId],
            scene.CommittedRoleOrder.Select(static slot => slot.Role.EntityId));

        (int centerX, int centerY) = WorldCameraProjection.ProjectTile(fixture.Map, 60, 108);
        WorldPuzzleProjection projection = WorldCameraProjection.FromHeroWorldPosition(
            fixture.Map, fixture.Puzzles, centerX, centerY, 800, 600);
        (int peerX, int peerY) = WorldCameraProjection.ProjectTile(fixture.Map, nearby.TileX, nearby.TileY);
        (Matrix4x4 Main, Matrix4x4 Shadow) expected = WorldRoleCameraMatrices.Create(
            fixture.Map, fixture.Puzzles, projection, peerX, peerY,
            (-1 - nearby.Direction) * 45f, 0.75f);
        Assert.Equal(expected, scene.RoleMatrices(nearby));

        scene.SetRemoteRoles(2, [farther]);
        scene.BuildFrame(800, 600, 1100);
        Assert.Equal([0u, farther.EntityId],
            scene.CommittedRoleOrder.Select(static slot => slot.Role.EntityId));
    }

    [Fact]
    public void InvalidHeroDirectionDoesNotReplaceTheCurrentFacing()
    {
        using WorldMapFixture fixture = new();
        WorldMapScene scene = new(fixture.Map, fixture.Puzzles, [], fixture.Scenery, 60, 108, 100, 1000);
        scene.BuildFrame(800, 600, 1000);
        scene.SetHeroDirection(4);
        (Matrix4x4 Main, Matrix4x4 Shadow) previous = scene.HeroMatrices;

        Assert.Throws<ArgumentOutOfRangeException>(() => scene.SetHeroDirection(8));
        Assert.Equal(previous, scene.HeroMatrices);
    }

    [Theory]
    [InlineData(-1, 60, -1.08f, 1f)]
    [InlineData(60, -1, 1.08f, 1f)]
    [InlineData(132, 132, 0f, -4.253333f)]
    public void OffMapHeroKeepsItsAnchorWhileCameraClampsToGroundBounds(int tileX, int tileY, float clipX, float clipY)
    {
        using WorldMapFixture fixture = new();
        WorldMapScene scene = new(fixture.Map, fixture.Puzzles, [], fixture.Scenery, 60, 108, 100, 1000);

        scene.SetCameraTile(tileX, tileY);
        IReadOnlyList<WorldMapLayer> layers = scene.BuildFrame(800, 600, 1000);

        Assert.NotNull(layers[1].Puzzle);
        Vector4 anchor = Vector4.Transform(new Vector4(0, 0, 0, 1), scene.HeroMatrices.Main);
        Assert.InRange(anchor.X, clipX - 0.00001f, clipX + 0.00001f);
        Assert.InRange(anchor.Y, clipY - 0.00001f, clipY + 0.00001f);
        scene.SetCameraTile(60, 108);
        Assert.NotEmpty(scene.BuildFrame(800, 600, 1001)[1].Puzzle!);
    }

    [Fact]
    public void MissingGroundPuzzle_LeavesGroundLayerEmptyAndKeepsOtherLayers()
    {
        using WorldMapFixture fixture = new(missingGround: true);
        WorldMapScene scene = new(fixture.Map, fixture.Puzzles, [], fixture.Scenery, 60, 108, 100, 1000);

        IReadOnlyList<WorldMapLayer> layers = scene.BuildFrame(800, 600, 1000);

        Assert.Null(fixture.Puzzles.Ground);
        Assert.Empty(layers[1].Puzzle!);
        Assert.NotEmpty(layers[0].Puzzle!);
        Assert.NotNull(layers[4].Scenery);
    }

    [Fact]
    public void PositionedPuzzle_AdvancesWithItsOwnFrameClock()
    {
        using WorldMapFixture fixture = new(twoFrameBackdrop: true);
        WorldMapScene scene = new(fixture.Map, fixture.Puzzles, [], fixture.Scenery, 60, 108, 100, 1000);

        IReadOnlyList<WorldPuzzleTile> first = scene.BuildFrame(800, 600, 1000)[0].Puzzle!;
        IReadOnlyList<WorldPuzzleTile> second = scene.BuildFrame(800, 600, 1100)[0].Puzzle!;
        IReadOnlyList<WorldPuzzleTile> third = scene.BuildFrame(800, 600, 1200)[0].Puzzle!;

        Assert.All(first, static tile => Assert.Equal(0, tile.FrameIndex));
        Assert.All(second, static tile => Assert.Equal(0, tile.FrameIndex));
        Assert.All(third, static tile => Assert.Equal(1, tile.FrameIndex));
    }
}
