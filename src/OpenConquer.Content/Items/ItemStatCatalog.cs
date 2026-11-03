using System.Globalization;
using System.Text;
using OpenConquer.Content.Encoding;

namespace OpenConquer.Content.Items;

public readonly record struct ItemBaseStats(byte RequiredLevel, short Life, short Mana);

public sealed class ItemStatCatalog
{
    public const string ItemTypePath = "ini/itemtype.dat";
    public const string ItemAddPath = "ini/ItemAdd.ini";

    private readonly Dictionary<uint, ItemBaseStats> _items;
    private readonly Dictionary<(uint Type, byte Level), ushort> _additions;

    private ItemStatCatalog(Dictionary<uint, ItemBaseStats> items,
        Dictionary<(uint Type, byte Level), ushort> additions)
    {
        _items = items;
        _additions = additions;
    }

    public bool TryGetItem(uint typeId, out ItemBaseStats stats) => _items.TryGetValue(typeId, out stats);

    public ushort GetAdditionLife(uint typeId, byte level) =>
        _additions.GetValueOrDefault((typeId, level));

    public static ItemStatCatalog Load(IClientContentSource source)
    {
        byte[] itemTypes = ContentReader.ReadRequiredBytes(source, ItemTypePath,
            ContentLookupMode.LooseOnly, 5 * 1024 * 1024);
        byte[] itemAdds = ContentReader.ReadRequiredBytes(source, ItemAddPath,
            ContentLookupMode.LooseOnly, 1024 * 1024);
        return Parse(itemTypes, itemAdds);
    }

    public static ItemStatCatalog Parse(ReadOnlySpan<byte> itemTypeBytes, ReadOnlySpan<byte> itemAddBytes)
    {
        Dictionary<uint, ItemBaseStats> items = ReadItemTypes(itemTypeBytes);
        Dictionary<(uint, byte), ushort> additions = ReadItemAdds(itemAddBytes);
        return new ItemStatCatalog(items, additions);
    }

    private static Dictionary<uint, ItemBaseStats> ReadItemTypes(ReadOnlySpan<byte> encoded)
    {
        byte[] decoded = encoded.ToArray();
        DelimitedTableCipher.DecodeInPlace(decoded);

        Dictionary<uint, ItemBaseStats> result = [];
        using StringReader reader = new(Encoding.Latin1.GetString(decoded));
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0)
            {
                continue;
            }

            string[] fields = line.Split("@@", StringSplitOptions.None);
            if (fields.Length < 21 ||
                !uint.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out uint type) ||
                !byte.TryParse(fields[4], NumberStyles.None, CultureInfo.InvariantCulture, out byte level) ||
                !short.TryParse(fields[19], NumberStyles.Integer, CultureInfo.InvariantCulture, out short life) ||
                !short.TryParse(fields[20], NumberStyles.Integer, CultureInfo.InvariantCulture, out short mana))
            {
                throw new InvalidDataException("Item type data contains an invalid stat record.");
            }

            result[type] = new ItemBaseStats(level, life, mana);
        }

        if (result.Count == 0)
        {
            throw new InvalidDataException("Item type data contains no records.");
        }

        return result;
    }

    private static Dictionary<(uint, byte), ushort> ReadItemAdds(ReadOnlySpan<byte> data)
    {
        Dictionary<(uint, byte), ushort> result = [];
        using StringReader reader = new(Encoding.Latin1.GetString(data));
        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            string[] fields = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length != 10 ||
                !uint.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out uint type) ||
                !byte.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out byte level) ||
                !ushort.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out ushort life))
            {
                throw new InvalidDataException("Item addition data contains an invalid record.");
            }

            for (int i = 3; i < fields.Length; i++)
            {
                if (!ushort.TryParse(fields[i], NumberStyles.None, CultureInfo.InvariantCulture, out _))
                {
                    throw new InvalidDataException("Item addition data contains an invalid payload.");
                }
            }

            result[(type, level)] = life;
        }

        if (result.Count == 0)
        {
            throw new InvalidDataException("Item addition data contains no records.");
        }

        return result;
    }
}
