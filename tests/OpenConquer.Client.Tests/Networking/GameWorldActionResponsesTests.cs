using OpenConquer.Networking.Game.Login;

namespace OpenConquer.Client.Tests.Networking;

public sealed class GameWorldActionResponsesTests
{
    [Theory]
    [InlineData(GameWorldAction10010.GetFriends, GameWorldAction10010.GetWeaponSkills)]
    [InlineData(GameWorldAction10010.GetWeaponSkills, GameWorldAction10010.GetMagic)]
    [InlineData(GameWorldAction10010.GetMagic, GameWorldAction10010.GetSyndicate)]
    [InlineData(GameWorldAction10010.ReportSilentInfo, GameWorldAction10010.GetStatistics)]
    public void AdmissionAndWorldUseTheSameSingleResponse(ushort incoming, ushort outgoing)
    {
        Assert.True(GameWorldActionResponses.TryForAcknowledgement(incoming, 1_000_000,
            GameSilentInfoReport.WithoutFiles, out GameWorldActionResponse first,
            out GameWorldActionResponse second));

        Assert.Equal(new GameWorldActionResponse(outgoing, 1_000_000, 0), first);
        Assert.Equal(default, second);
    }

    [Fact]
    public void ItemAcknowledgementProducesFriendsBeforePkMode()
    {
        Assert.True(GameWorldActionResponses.TryForAcknowledgement(GameWorldAction10010.GetItems,
            1_000_000, GameSilentInfoReport.WithoutFiles,
            out GameWorldActionResponse first, out GameWorldActionResponse second));

        Assert.Equal(new GameWorldActionResponse(GameWorldAction10010.GetFriends, 1_000_000, 0), first);
        Assert.Equal(new GameWorldActionResponse(GameWorldAction10010.SetPkMode, 1_000_000, 3), second);
    }

    [Fact]
    public void SyndicateAcknowledgementUsesSessionSilentInfo()
    {
        Assert.True(GameWorldActionResponses.TryForAcknowledgement(GameWorldAction10010.GetSyndicate,
            1_000_000, new GameSilentInfoReport(0xAABBCCDD, 7),
            out GameWorldActionResponse first, out GameWorldActionResponse second));

        Assert.Equal(new GameWorldActionResponse(GameWorldAction10010.ReportSilentInfo,
            0, 0xAABBCCDD, 7), first);
        Assert.Equal(default, second);
    }

    [Fact]
    public void UnsupportedActionDoesNotCreateResponses()
    {
        Assert.False(GameWorldActionResponses.TryForAcknowledgement(GameWorldAction10010.PositionCorrection,
            1_000_000, GameSilentInfoReport.WithoutFiles,
            out GameWorldActionResponse first, out GameWorldActionResponse second));
        Assert.Equal(default, first);
        Assert.Equal(default, second);
    }
}
