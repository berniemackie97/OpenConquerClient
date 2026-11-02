using OpenConquer.Content.Map;

namespace OpenConquer.Content.Tool.Import;

internal static class MapAssetCompiler
{
    public static MapAsset Compile(IClientContentSource source, uint mapDataId)
    {
        GameMapContent sourceMap = GameMapContent.Load(source, GameMapCatalog.Load(source), mapDataId);
        MapImportIds ids = new();
        GameMapCellGrid grid = sourceMap.CellGrid;
        if (sourceMap.Scene.ExitMarkers.Count != 0)
        {
            throw new InvalidDataException("Map exits are not represented by the compiled map schema.");
        }

        GameMapCell[] cells = new GameMapCell[checked(grid.Width * grid.Height)];
        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                cells[y * grid.Width + x] = grid.GetCell(x, y);
            }
        }

        List<MapObject> objects = [];
        foreach (GameMapSceneryRecord record in sourceMap.Scene.Scenery)
        {
            if (record.TypeCode == 4 && GameMapSceneryAniBindings.IsEmptyAnimationRecord(record))
            {
                continue;
            }

            if (record.ResourcePath is not { } path || record.TileX is not int x || record.TileY is not int y)
            {
                throw new InvalidDataException("Map scenery has no supported resource or position.");
            }

            objects.Add(record.TypeCode switch
            {
                1 => new MapTerrainObject(ids.Register(path), x, y),
                4 when record.PartName is { } section => new MapAnimatedObject(ids.Register(path), section, x, y),
                _ => throw new InvalidDataException(
                    $"Map scenery type {record.TypeCode} requires a defined compiled representation."),
            });
        }

        List<MapLayer> layers = [];
        foreach (GameMapPositionedOverlayRecord record in sourceMap.Scene.PositionedOverlays)
        {
            List<MapLayerEntry> children = [];
            foreach (GameMapPositionedOverlayEntry child in record.Entries)
            {
                children.Add(child switch
                {
                    GameMapPuzzleOverlayEntry puzzle => new MapPuzzleLayerEntry(ids.Register(puzzle.PuzzlePath)),
                    GameMapAnimatedOverlayEntry animation => new MapAnimationLayerEntry(
                        ids.Register(animation.ResourcePath), animation.PartName,
                        animation.TileX, animation.TileY, animation.RenderOffsetX, animation.RenderOffsetY,
                        animation.FrameIntervalMilliseconds, animation.AdditionalValue),
                    _ => throw new InvalidDataException("Map contains an unsupported layer entry."),
                });
            }

            layers.Add(new MapLayer(record.InsertIndex, record.HeaderInt0, record.HeaderInt1,
                Array.AsReadOnly(children.ToArray())));
        }

        return new MapAsset(mapDataId, ids.Register(grid.PuzzlePath), grid.Width, grid.Height,
            Array.AsReadOnly(cells), Array.AsReadOnly(objects.ToArray()), Array.AsReadOnly(layers.ToArray()));
    }

    private sealed class MapImportIds
    {
        private readonly Dictionary<string, string> _sourceById = new(StringComparer.Ordinal);

        public string Register(string sourcePath)
        {
            ContentPath.Validate(sourcePath);
            string path = ContentPath.ToKey(sourcePath);
            int separator = path.LastIndexOf('/');
            int extension = path.LastIndexOf('.');
            if (extension <= separator + 1)
            {
                throw new InvalidDataException($"Source asset '{sourcePath}' has no recognized extension.");
            }

            string id = path[..extension];
            MapAssetId.Validate(id);
            if (_sourceById.TryGetValue(id, out string? previous) &&
                !string.Equals(previous, path, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Distinct input assets '{previous}' and '{path}' have the same compiled ID.");
            }

            _sourceById[id] = path;
            return id;
        }
    }

    public static void Compile(string sourceRoot, uint mapDataId, string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        PackagedClientContentSource source = PackagedClientContentSource.Open(sourceRoot);
        MapAsset map = Compile(source, mapDataId);

        string destination = Path.GetFullPath(destinationPath);
        if (File.Exists(destination) || Directory.Exists(destination))
        {
            throw new IOException($"Compiled map destination '{destination}' already exists.");
        }

        string directory = Path.GetDirectoryName(destination) ??
            throw new InvalidOperationException("Compiled map destination has no parent directory.");
        HostFileSystemGuard.RequireDirectory(directory, "compiled map destination directory");
        string staging = Path.Combine(directory, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (FileStream output = new(staging, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                MapAssetCodec.Write(map, output);
                output.Flush(flushToDisk: true);
            }

            File.Move(staging, destination);
        }
        finally
        {
            if (File.Exists(staging))
            {
                File.Delete(staging);
            }
        }
    }
}
