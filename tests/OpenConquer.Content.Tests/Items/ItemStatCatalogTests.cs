using System.Text;
using OpenConquer.Content.Items;

namespace OpenConquer.Content.Tests.Items;

public sealed class ItemStatCatalogTests
{
    private const string FirstEncodedItemTypeRow =
        "mAuPet6wHqBj+RrWV0eJPyoAosQpSgR2wJgYx4sTLvfBh/A0kVQQ183bXT1uLgKS4wUF3GvUFKr1ggwE5jYJlcvDtIdQEaDMIM0Oji2w6l0C7k91j355oYbXxYS9LyDgIsWkzuWnKfHSTjFaSm+1y8R0Scj4sRRv/e6tuNeBC1LtC0753rAeEVOzT/dHDwWobQCixClKBHbAmBjHixMu98GH8DSRVBDXzdtdPW4uApLjB8RfbNQIkvVgyYfmNkmtyyN1h1cfoPRQzTtf";

    [Fact]
    public void DecodesOriginalItemTypeRecordAndItemAdditionPayload()
    {
        ItemStatCatalog catalog = ItemStatCatalog.Parse(
            Convert.FromBase64String(FirstEncodedItemTypeRow),
            Encoding.ASCII.GetBytes("120020 9 239 0 0 23 0 0 0 0\r\n"));

        Assert.True(catalog.TryGetItem(50000, out ItemBaseStats stats));
        Assert.Equal(new ItemBaseStats(0, 0, 0), stats);
        Assert.Equal((ushort)239, catalog.GetAdditionLife(120020, 9));
        Assert.Equal((ushort)0, catalog.GetAdditionLife(120020, 8));
        Assert.False(catalog.TryGetItem(50001, out _));
    }

    [Fact]
    public void RejectsMalformedStatTableRowsInsteadOfFillingGuessedValues()
    {
        byte[] row = EncodeItemType("120020@@MissingFields\r\n");
        byte[] addition = Encoding.ASCII.GetBytes("120020 9 239 0 0 23 0 0 0 0\r\n");
        Assert.Throws<InvalidDataException>(() => ItemStatCatalog.Parse(row, addition));
        Assert.Throws<InvalidDataException>(() => ItemStatCatalog.Parse(
            Convert.FromBase64String(FirstEncodedItemTypeRow), Encoding.ASCII.GetBytes("1 2 3\n")));
    }

    internal static byte[] EncodeItemType(string text)
    {
        byte[] encoded = Encoding.Latin1.GetBytes(text);
        uint seed = 0x2537;
        byte[] keys = new byte[128];
        for (int i = 0; i < keys.Length; i++)
        {
            seed = unchecked(seed * 214013u + 2531011u);
            keys[i] = (byte)(seed >> 16);
        }

        for (int i = 0; i < encoded.Length; i++)
        {
            int shift = i & 7;
            byte value = encoded[i];
            encoded[i] = (byte)((shift == 0
                ? value
                : ((value << shift) | (value >> (8 - shift)))) ^ keys[i % 128]);
        }

        return encoded;
    }
}
