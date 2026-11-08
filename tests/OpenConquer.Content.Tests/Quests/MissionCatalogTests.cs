using OpenConquer.Content.Quests;

namespace OpenConquer.Content.Tests;

public sealed class MissionCatalogTests
{
    [Fact]
    public void Load_DeduplicatesAndSortsSelectedRows()
    {
        MissionCatalog catalog = Load("""
            [TotalMission]
            TotalMission=5
            [1]
            MissionId=928
            [2]
            MissionId=600
            [3]
            MissionId=0X3A0
            [4]
            MissionId=928
            [5]
            MissionId=601
            [6]
            MissionId=777
            """);

        Assert.Equal([600, 601, 928], catalog.MissionIds.ToArray());
    }

    [Fact]
    public void Load_UsesCaseInsensitiveSectionAndKeyNames()
    {
        MissionCatalog catalog = Load("""
            [totalmission]
            totalmission=2
            [1]
            missionid=601
            [2]
            MISSIONID=600
            """);

        Assert.Equal([600, 601], catalog.MissionIds.ToArray());
    }

    [Theory]
    [InlineData("")]
    [InlineData("[TotalMission]\nTotalMission=0\n")]
    [InlineData("[TotalMission]\nTotalMission=-1\n")]
    [InlineData("[TotalMission]\nTotalMission=nope\n")]
    [InlineData("[TotalMission]\nTotalMission=4097\n")]
    [InlineData("[TotalMission]\nTotalMission=1\n[1]\nMissionId=0\n")]
    [InlineData("[TotalMission]\nTotalMission=1\n[1]\nMissionId=-1\n")]
    [InlineData("[TotalMission]\nTotalMission=1\n[1]\nMissionId=garbage\n")]
    [InlineData("[TotalMission]\nTotalMission=1\n[1]\nMissionId=0x\n")]
    [InlineData("[TotalMission]\nTotalMission=2\n[1]\nMissionId=600\n")]
    public void Load_FailsOnMissingOrInvalidCatalogValues(string text) =>
        Assert.Throws<InvalidDataException>(() => Load(text));

    [Fact]
    public void Load_RejectsTooManyDistinctIdsButAllowsDuplicateRows()
    {
        string duplicateRows = Build(4096, _ => "600");
        Assert.Equal([600], Load(duplicateRows).MissionIds.ToArray());

        string distinctRows = Build(MissionCatalog.MaximumMissionIds + 1,
            index => (index + 600).ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Throws<InvalidDataException>(() => Load(distinctRows));
    }

    [Fact]
    public void Load_RejectsOversizedSource()
    {
        string oversized = "[TotalMission]\nTotalMission=1\n[1]\nMissionId=600\n" +
            new string(' ', 512 * 1024);

        Assert.Throws<InvalidDataException>(() => Load(oversized));
    }

    [Fact]
    public void Load_RejectsMissingLooseFile()
    {
        using TemporaryContentDirectory directory = new();
        Assert.Throws<FileNotFoundException>(() => MissionCatalog.Load(new ClientContentRoot(directory.RootPath)));
    }

    private static MissionCatalog Load(string text)
    {
        using TemporaryContentDirectory directory = new();
        directory.WriteFile(MissionCatalog.RelativePath, text);
        return MissionCatalog.Load(new ClientContentRoot(directory.RootPath));
    }

    private static string Build(int count, Func<int, string> id)
    {
        System.Text.StringBuilder text = new();
        text.Append("[TotalMission]\nTotalMission=").Append(count).Append('\n');
        for (int index = 0; index < count; index++)
        {
            text.Append('[').Append(index + 1).Append("]\nMissionId=").Append(id(index)).Append('\n');
        }

        return text.ToString();
    }
}
