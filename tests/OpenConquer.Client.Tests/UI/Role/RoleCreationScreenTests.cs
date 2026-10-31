using OpenConquer.Client.UI.Role;

namespace OpenConquer.Client.Tests.UI.Role;

public sealed class RoleCreationScreenTests
{
    [Theory]
    [InlineData(0, 100)]
    [InlineData(1, 100)]
    [InlineData(2, 10)]
    [InlineData(3, 10)]
    [InlineData(4, 40)]
    [InlineData(5, 40)]
    [InlineData(6, 20)]
    [InlineData(7, 20)]
    [InlineData(8, 50)]
    [InlineData(9, 50)]
    [InlineData(10, 60)]
    [InlineData(11, 60)]
    public void ProfessionFromVocation_MapsAllTwelveRetailSelections(int vocation, ushort profession)
    {
        Assert.Equal(profession, RoleCreationScreen.ProfessionFromVocation(vocation));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(12)]
    [InlineData(int.MaxValue)]
    public void ProfessionFromVocation_RejectsInvalidSelections(int vocation)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RoleCreationScreen.ProfessionFromVocation(vocation));
    }
}
