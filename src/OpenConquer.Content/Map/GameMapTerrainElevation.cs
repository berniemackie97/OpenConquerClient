namespace OpenConquer.Content.Map;

/// <summary>
/// Per-tile tail-link elevation: the cell-grid base elevation plus every attached terrain-object
/// cell delta, composed the way the 5517 client attaches terrain object groups at map load.
/// </summary>
/// <remarks>
/// <c>CMapSceneryComponent_AttachTerrainObjectGroupToTileGrid</c> (0x635FC5): for each terrain object of
/// a group, in map order, object anchor = group anchor + object offset (0x63F4D3); per-cell record
/// (x, y) at index <c>width * y + x</c> (0x644856) attaches to tile <c>anchor - (x, y)</c> with
/// elevation <c>int16(previousTail + delta)</c>. Out-of-grid tiles have no entry (0x61D9C8).
/// </remarks>
public sealed class GameMapTerrainElevation
{
    private readonly short[] _base;
    private readonly short[] _tail;

    private GameMapTerrainElevation(int width, int height, short[] baseElevation, short[] tail)
    {
        Width = width;
        Height = height;
        _base = baseElevation;
        _tail = tail;
    }

    public int Width
    {
        get;
    }

    public int Height
    {
        get;
    }

    public static GameMapTerrainElevation Compose(GameMapCellGrid grid,
        IReadOnlyList<GameMapTerrainScenePlacement> placements)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(placements);
        int width = grid.Width;
        int height = grid.Height;
        short[] baseElevation = new short[checked(width * height)];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                baseElevation[y * width + x] = grid.GetCell(x, y).Elevation;
            }
        }

        short[] tail = (short[])baseElevation.Clone();
        foreach (GameMapTerrainScenePlacement placement in placements)
        {
            if (placement.Scenery.TileX is not int groupX || placement.Scenery.TileY is not int groupY)
            {
                continue;
            }

            foreach (GameMapTerrainObjectBinding binding in placement.Objects)
            {
                GameMapTerrainObject terrain = binding.Terrain;
                int anchorX = unchecked(groupX + terrain.AnchorOffsetX);
                int anchorY = unchecked(groupY + terrain.AnchorOffsetY);
                for (int cellY = 0; cellY < terrain.GridHeight; cellY++)
                {
                    for (int cellX = 0; cellX < terrain.GridWidth; cellX++)
                    {
                        int tileX = unchecked(anchorX - cellX);
                        int tileY = unchecked(anchorY - cellY);
                        if ((uint)tileX >= (uint)width || (uint)tileY >= (uint)height)
                        {
                            continue;
                        }

                        int index = tileY * width + tileX;
                        short delta = terrain.Cells[cellY * terrain.GridWidth + cellX].ElevationDelta;
                        tail[index] = unchecked((short)(tail[index] + delta));
                    }
                }
            }
        }

        return new GameMapTerrainElevation(width, height, baseElevation, tail);
    }

    /// <summary>Tail elevation minus base elevation, or false for a tile outside the grid.</summary>
    public bool TryGetAdjusted(int tileX, int tileY, out int adjusted)
    {
        if ((uint)tileX >= (uint)Width || (uint)tileY >= (uint)Height)
        {
            adjusted = 0;
            return false;
        }

        int index = tileY * Width + tileX;
        adjusted = _tail[index] - _base[index];
        return true;
    }

    /// <summary>Tail-link elevation, or false for a tile outside the grid.</summary>
    public bool TryGetTail(int tileX, int tileY, out int tail)
    {
        if ((uint)tileX >= (uint)Width || (uint)tileY >= (uint)Height)
        {
            tail = 0;
            return false;
        }

        tail = _tail[tileY * Width + tileX];
        return true;
    }
}
