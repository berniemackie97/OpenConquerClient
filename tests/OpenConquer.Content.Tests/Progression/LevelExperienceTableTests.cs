using System.Text;
using OpenConquer.Content.Progression;

namespace OpenConquer.Content.Tests.Progression;

public sealed class LevelExperienceTableTests
{
    [Fact]
    public void ResolvesPlayerRequirementsByLevelWithoutMixingOtherTables()
    {
        LevelExperienceTable table = LevelExperienceTable.Parse(Encode(
            "0 1 120 2 3\r\n2 1 99999 0 0\r\n0 2 180 2 3\r\n" +
            "0 3 8589134588 2 3\r\n0 4 0 2 3\r\n"));

        Assert.Equal(120UL, table.GetNextLevelExperience(1));
        Assert.Equal(180UL, table.GetNextLevelExperience(2));
        Assert.Equal(8_589_134_588UL, table.GetNextLevelExperience(3));
        Assert.Equal(0UL, table.GetNextLevelExperience(4));
        Assert.Equal(0UL, table.GetNextLevelExperience(5));
        Assert.Equal(0UL, table.GetNextLevelExperience(0));
        Assert.Equal(0UL, table.GetNextLevelExperience(151));
    }

    [Fact]
    public void LoadsTheEncodedTableFromTheContentSource()
    {
        using TemporaryContentDirectory directory = new();
        directory.WriteFile(LevelExperienceTable.RelativePath, Encode("0 1 120 2 3\r\n"));

        LevelExperienceTable table = LevelExperienceTable.Load(new ClientContentRoot(directory.RootPath));

        Assert.Equal(120UL, table.GetNextLevelExperience(1));
    }

    [Theory]
    [InlineData("0 2 120 2 3\r\n")]
    [InlineData("0 1 -1 2 3\r\n")]
    [InlineData("0 1 120 2\r\n")]
    [InlineData("0 1 120 2 3\r\n0 3 180 2 3\r\n")]
    [InlineData("1 1 120 2 3\r\n")]
    [InlineData("0 151 120 2 3\r\n")]
    [InlineData("0 1 120 2 invalid\r\n")]
    public void RejectsInvalidOrMissingPlayerProgression(string text)
    {
        Assert.Throws<InvalidDataException>(() => LevelExperienceTable.Parse(Encode(text)));
    }

    [Fact]
    public void RejectsEmptyAndOversizedTables()
    {
        Assert.Throws<InvalidDataException>(() => LevelExperienceTable.Parse([]));
        Assert.Throws<InvalidDataException>(() => LevelExperienceTable.Parse(new byte[256 * 1024 + 1]));
    }

    private static byte[] Encode(string text)
    {
        byte[] payload = Encoding.ASCII.GetBytes(text);
        Span<byte> keys = stackalloc byte[128];
        uint state = 0x4D2;

        for (int index = 0; index < keys.Length; index++)
        {
            state = unchecked(state * 214013 + 2531011);
            keys[index] = (byte)(state >> 16);
        }

        for (int index = 0; index < payload.Length; index++)
        {
            int rotation = index & 7;
            byte value = payload[index];
            payload[index] = (byte)(((value << rotation) | (value >> (8 - rotation))) ^ keys[index % keys.Length]);
        }

        return payload;
    }
}
