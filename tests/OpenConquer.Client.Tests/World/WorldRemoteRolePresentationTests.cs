using OpenConquer.Client.Diagnostics;
using OpenConquer.Client.World;
using OpenConquer.Networking.Game.World;

namespace OpenConquer.Client.Tests.World;

public sealed class WorldRemoteRolePresentationTests
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task VisibleIdentitySelectionReusesAppearanceAndEnforcesTheResourceLimit()
    {
        WorldRemoteRoles roles = new();
        for (int i = 0; i < WorldRemoteRolePresentation.MaximumPreparedAppearances + 2; i++)
        {
            roles.Upsert(Player(1_000_001u + (uint)i, 1001u + (uint)i));
        }

        uint reusedId = 1_001_000;
        roles.Upsert(Player(reusedId, 1001));
        await using WorldRemoteRolePresentation presentation = new(
            static (_, token) =>
            {
                token.ThrowIfCancellationRequested();
                return null;
            },
            ClientDiagnostics.Silent);

        presentation.Synchronize(roles, 61, 101);

        Assert.Equal(WorldRemoteRolePresentation.MaximumPreparedAppearances + 1,
            presentation.SelectedRoleCount);
        Assert.True(presentation.Includes(1_000_001));
        Assert.True(presentation.Includes(reusedId));
        Assert.False(presentation.Includes(1_000_001u +
            (uint)WorldRemoteRolePresentation.MaximumPreparedAppearances));

        Assert.True(roles.Remove(1_000_001));
        Assert.True(roles.Remove(reusedId));
        presentation.Synchronize(roles, 61, 101);
        Assert.False(presentation.Includes(1_000_001));
        Assert.False(presentation.Includes(reusedId));
        Assert.True(presentation.Includes(1_000_001u +
            (uint)WorldRemoteRolePresentation.MaximumPreparedAppearances));
    }

    [Fact]
    public async Task AppearanceReplacementCancelsObsoletePreparation()
    {
        TaskCompletionSource<CancellationToken> started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        WorldRemoteRoles roles = new();
        roles.Upsert(Player(1_000_001, 1002));
        await using WorldRemoteRolePresentation presentation = new(
            (_, token) =>
            {
                started.TrySetResult(token);
                token.WaitHandle.WaitOne();
                token.ThrowIfCancellationRequested();
                return null;
            },
            ClientDiagnostics.Silent);

        presentation.Synchronize(roles, 61, 101);
        CancellationToken pending = await started.Task.WaitAsync(s_timeout, TestContext.Current.CancellationToken);
        Assert.False(pending.IsCancellationRequested);

        Assert.True(roles.Remove(1_000_001));
        presentation.Synchronize(roles, 61, 101);

        Assert.True(pending.IsCancellationRequested);
    }

    [Fact]
    public async Task DisposingAppearanceOwnerCancelsAndDrainsTheSinglePendingRead()
    {
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource canceled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        WorldRemoteRoles roles = new();
        roles.Upsert(Player(1_000_001, 1002));
        WorldRemoteRolePresentation presentation = new(
            (_, token) =>
            {
                using CancellationTokenRegistration registration = token.Register(() => canceled.TrySetResult());
                started.SetResult();
                token.WaitHandle.WaitOne();
                token.ThrowIfCancellationRequested();
                return null;
            },
            ClientDiagnostics.Silent);

        presentation.Synchronize(roles, 61, 101);
        await started.Task.WaitAsync(s_timeout, TestContext.Current.CancellationToken);
        await presentation.DisposeAsync();
        await canceled.Task.WaitAsync(s_timeout, TestContext.Current.CancellationToken);
        await presentation.DisposeAsync();
    }

    [Fact]
    public void AppearanceIdentityUsesTheSameEquipmentSourcesForAllPeers()
    {
        GamePlayerSpawn10014 player = Player(1_000_001, 1002) with
        {
            HairComposite = 335,
            ArmorItemType = 130003,
            LeftHandItemType = 420033
        };
        WorldRoleAppearance appearance = WorldRoleAppearance.From(player);

        Assert.Equal(2u, appearance.BodyFamily);
        Assert.Equal(new WorldRoleSources(2_130_000, 2_119_335, 0, 420030),
            appearance.Sources);
        Assert.Equal(appearance, WorldRoleAppearance.From(player with
        {
            EntityId = 1_000_002
        }));
    }

    private static GamePlayerSpawn10014 Player(uint id, uint appearance) =>
        new(id, new byte[] { (byte)'P' }, appearance, 335, 61, 101, 0, 100, 250, 120,
            0, 0, 0, 0, 0, 0);
}
