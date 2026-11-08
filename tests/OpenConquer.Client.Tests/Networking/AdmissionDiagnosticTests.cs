using OpenConquer.Client.UI.Role;
using OpenConquer.Networking.Game.Login;

namespace OpenConquer.Client.Tests.Networking;

public sealed class AdmissionDiagnosticTests
{
    [Fact]
    public void AdmissionRecordsExcludePlayerAndTransportDetails()
    {
        GameCharacterInfo1006 character = new()
        {
            EntityId = 123456789,
            PlayerNameBytes = "PRIVATE-NAME"u8.ToArray()
        };
        GameWorldEntrySnapshot entry = new(character, 1002, 1010, 0, 1, 61, 101, [], 3, LocalAddress: 123456789);
        GameLoginHistory2078 history = new(123456789, 1, "PRIVATE-LOCATION"u8.ToArray());
        GameExistingCharacterPrelude prelude = new(history, new GameServerState2079(123456789));
        RoleCreationOutcome outcome = new(RoleCreationOutcomeKind.Failed, Refusal: "PRIVATE-REFUSAL",
            World: entry, Failure: new IOException("/private/ticket"));

        Assert.Equal(nameof(GameCharacterInfo1006), character.ToString());
        Assert.Equal(nameof(GameWorldEntrySnapshot), entry.ToString());
        Assert.Equal(nameof(GameLoginHistory2078), history.ToString());
        Assert.Equal(nameof(GameExistingCharacterPrelude), prelude.ToString());
        Assert.Equal("RoleCreationOutcome { Kind = Failed }", outcome.ToString());
    }
}
