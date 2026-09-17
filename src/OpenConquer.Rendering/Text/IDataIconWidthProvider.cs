namespace OpenConquer.Rendering.Text;

/// <summary>
/// Resolves the measured width of one native #NN data icon.
/// </summary>
internal interface IDataIconWidthProvider
{
    bool TryGetWidth(byte dataIconIndex, out int widthPixels);
}
