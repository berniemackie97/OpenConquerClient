using System.Globalization;
using System.Text;
using OpenConquer.Content.Magic;

namespace OpenConquer.Content.Tests.Magic;

public sealed class MagicTypeFileTests
{
    [Fact]
    public void Decode_UsesNativeRetailCipher()
    {
        byte[] encoded = Convert.FromHexString("9C0B8F7AD9B0421123538EF7500105A2356A1A667B0C1876");

        MagicTypeFile.DecodeInPlace(encoded);

        Assert.Equal("1000@@1000@@1@@Thunder@@", Encoding.ASCII.GetString(encoded));
    }

    [Fact]
    public void Parse_ResolvesNativeTypeAndLevelKeys()
    {
        byte[] encoded = Encode(
            CreateRow(1000, 0, 2000),
            CreateRow(1000, 1, 113060, requiredCharacterLevel: 10),
            CreateRow(1415, 0, 0, encodedProfessionRequirement: 1006, requiredSubprofessionPhase: 1));

        MagicTypeFile file = MagicTypeFile.Parse(encoded);

        Assert.Equal(3, file.Count);

        Assert.True(file.TryGet(1000, 0, out MagicTypeRecord? elementary));
        Assert.Equal(1000u, elementary.Type);
        Assert.Equal(0u, elementary.Level);
        Assert.Equal(2000u, elementary.RequiredExperience);

        Assert.True(file.TryGet(1000, 1, out MagicTypeRecord? learned));
        Assert.Equal(113060u, learned.RequiredExperience);
        Assert.Equal(10u, learned.RequiredCharacterLevel);

        Assert.True(file.TryGet(1415, 0, out MagicTypeRecord? subprofession));
        Assert.Equal(1006u, subprofession.EncodedProfessionRequirement);
        Assert.Equal(6u, subprofession.RequiredSubprofessionClass);
        Assert.Equal(1u, subprofession.RequiredSubprofessionPhase);

        Assert.False(file.TryGet(1000, 2, out _));
        Assert.False(file.TryGet(0, 0, out _));
    }

    [Fact]
    public void Parse_PreservesAllSeventeenVerifiedNativeWords()
    {
        string[] fields = new string[48];

        for (int index = 0; index < fields.Length; index++)
        {
            fields[index] = (1000 + index).ToString(CultureInfo.InvariantCulture);
        }

        fields[3] = "NativeMagic";

        MagicTypeFile file = MagicTypeFile.Parse(Encode(string.Join("@@", fields) + "@@"));

        Assert.True(file.TryGet(1001, 1008, out MagicTypeRecord? record));

        int[] sourceIndexes =
        [
            23, 47, 4, 15, 13, 18, 8, 20,
            9, 32, 10, 14, 42, 41, 29, 22, 1
        ];

        Assert.Equal(MagicTypeRecord.NativeWordCount, sourceIndexes.Length);

        for (int index = 0; index < sourceIndexes.Length; index++)
        {
            Assert.Equal((uint)(1000 + sourceIndexes[index]), record.GetNativeWord(index));
        }
    }

    [Fact]
    public void Parse_PreservesSignedNativeFieldBits()
    {
        string[] fields = CreateRow(1000, 0, 2000).Split("@@", StringSplitOptions.None);
        fields[10] = "-32768";

        MagicTypeFile file = MagicTypeFile.Parse(Encode(string.Join("@@", fields)));

        Assert.True(file.TryGet(1000, 0, out MagicTypeRecord? record));
        Assert.Equal(unchecked((uint)-32768), record.GetNativeWord(10));
    }

    [Fact]
    public void Parse_UsesLowThirtyTwoBitsOfNativeLookupKey()
    {
        byte[] encoded = Encode(CreateRow(uint.MaxValue, 2, 100));

        MagicTypeFile file = MagicTypeFile.Parse(encoded);

        Assert.True(file.TryGet(uint.MaxValue, 2, out MagicTypeRecord? record));
        Assert.Equal(uint.MaxValue, record.Type);
        Assert.Equal(2u, record.Level);
    }

    [Fact]
    public void Parse_RejectsDuplicateNativeKeys()
    {
        byte[] encoded = Encode(
            CreateRow(1000, 0, 2000),
            CreateRow(1000, 0, 3000));

        Assert.Throws<InvalidDataException>(() => MagicTypeFile.Parse(encoded));
    }

    [Fact]
    public void Parse_RejectsMalformedRecord()
    {
        string incomplete = CreateRow(1000, 0, 2000)[..^2];

        Assert.Throws<InvalidDataException>(() => MagicTypeFile.Parse(Encode(incomplete)));
    }

    [Fact]
    public void Parse_RejectsInvalidNumericField()
    {
        string[] fields = CreateRow(1000, 0, 2000).Split("@@", StringSplitOptions.None);
        fields[18] = "invalid";

        Assert.Throws<InvalidDataException>(() => MagicTypeFile.Parse(Encode(string.Join("@@", fields))));
    }

    [Fact]
    public void Parse_RejectsEmptyAndOversizedContent()
    {
        Assert.Throws<InvalidDataException>(() => MagicTypeFile.Parse(Array.Empty<byte>()));
        Assert.Throws<InvalidDataException>(() => MagicTypeFile.Parse(new byte[1024 * 1024 + 1]));
    }

    [Fact]
    public void Load_ReadsLooseRetailContent()
    {
        using TemporaryContentDirectory directory = new();
        directory.WriteFile(MagicTypeFile.RelativePath, Encode(CreateRow(1000, 0, 2000)));

        MagicTypeFile file = MagicTypeFile.Load(new ClientContentRoot(directory.RootPath));

        Assert.Equal(1, file.Count);
        Assert.True(file.TryGet(1000, 0, out MagicTypeRecord? record));
        Assert.Equal(2000u, record.RequiredExperience);
    }

    private static string CreateRow(uint type, uint level, uint requiredExperience, uint requiredCharacterLevel = 0,
        uint encodedProfessionRequirement = 0, uint requiredSubprofessionPhase = 0)
    {
        string[] fields = new string[48];
        Array.Fill(fields, "0");

        fields[1] = unchecked((int)type).ToString(CultureInfo.InvariantCulture);
        fields[3] = "NativeMagic";
        fields[8] = level.ToString(CultureInfo.InvariantCulture);
        fields[18] = requiredExperience.ToString(CultureInfo.InvariantCulture);
        fields[20] = requiredCharacterLevel.ToString(CultureInfo.InvariantCulture);
        fields[41] = encodedProfessionRequirement.ToString(CultureInfo.InvariantCulture);
        fields[42] = requiredSubprofessionPhase.ToString(CultureInfo.InvariantCulture);

        return string.Join("@@", fields) + "@@";
    }

    private static byte[] Encode(params string[] rows)
    {
        byte[] encoded = Encoding.ASCII.GetBytes(string.Join("\r\n", rows));
        Span<byte> key = stackalloc byte[128];
        uint state = 0x2537;

        for (int index = 0; index < key.Length; index++)
        {
            state = unchecked(state * 214013 + 2531011);
            key[index] = (byte)(state >> 16);
        }

        for (int index = 0; index < encoded.Length; index++)
        {
            int rotation = index & 7;
            byte value = encoded[index];
            byte rotated = unchecked((byte)((value << rotation) | (value >> (8 - rotation))));
            encoded[index] = (byte)(rotated ^ key[index % key.Length]);
        }

        return encoded;
    }
}
