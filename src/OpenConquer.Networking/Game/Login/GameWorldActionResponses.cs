namespace OpenConquer.Networking.Game.Login;

internal readonly record struct GameWorldActionResponse(
    ushort Action, uint EntityId, uint ParameterPair, uint? TimestampOverride = null);

internal static class GameWorldActionResponses
{
    internal static bool TryForAcknowledgement(ushort acknowledgement, uint heroId,
        GameSilentInfoReport silentInfo, out GameWorldActionResponse first,
        out GameWorldActionResponse second)
    {
        second = default;
        first = acknowledgement switch
        {
            GameWorldAction10010.GetItems =>
                new(GameWorldAction10010.GetFriends, heroId, 0),
            GameWorldAction10010.GetFriends =>
                new(GameWorldAction10010.GetWeaponSkills, heroId, 0),
            GameWorldAction10010.GetWeaponSkills =>
                new(GameWorldAction10010.GetMagic, heroId, 0),
            GameWorldAction10010.GetMagic =>
                new(GameWorldAction10010.GetSyndicate, heroId, 0),
            GameWorldAction10010.GetSyndicate =>
                new(GameWorldAction10010.ReportSilentInfo, 0, silentInfo.Checksum, silentInfo.Version),
            GameWorldAction10010.ReportSilentInfo =>
                new(GameWorldAction10010.GetStatistics, heroId, 0),
            _ => default,
        };
        if (first.Action == 0)
        {
            return false;
        }

        if (acknowledgement == GameWorldAction10010.GetItems)
        {
            second = new(GameWorldAction10010.SetPkMode, heroId, 3);
        }

        return true;
    }
}
