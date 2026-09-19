namespace OpenConquer.Rendering.Text.Layout;

/// <summary>
/// Contains measured native text dimensions, ordered drawable layout items, and the authoritative source that produced them.
/// </summary>
internal sealed class NativeTextLayout
{
    private readonly NativeTextLayoutItem[] _items;

    public NativeTextLayout(NativeTextLayoutSource source, int widthPixels, int heightPixels, IEnumerable<NativeTextLayoutItem> items)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(widthPixels);
        ArgumentNullException.ThrowIfNull(items);

        Source = source;
        WidthPixels = widthPixels;
        HeightPixels = heightPixels;
        _items = items.ToArray();
    }

    public NativeTextLayoutSource Source
    {
        get;
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
