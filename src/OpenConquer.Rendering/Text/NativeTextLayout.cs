namespace OpenConquer.Rendering.Text;

/// <summary>
/// Contains measured native text dimensions and ordered drawable layout items.
/// </summary>
internal sealed class NativeTextLayout
{
    private readonly NativeTextLayoutItem[] _items;

    public NativeTextLayout(int widthPixels, int heightPixels, IEnumerable<NativeTextLayoutItem> items)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(widthPixels);
        ArgumentNullException.ThrowIfNull(items);

        WidthPixels = widthPixels;
        HeightPixels = heightPixels;
        _items = items.ToArray();
    }

    public int WidthPixels
    {
        get;
    }

    public int HeightPixels
    {
        get;
    }

    public ReadOnlyMemory<NativeTextLayoutItem> Items => _items;
}
