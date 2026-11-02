using System.Diagnostics;
using OpenConquer.Content.Map;
using OpenConquer.Content.Tool.Manifest;
using OpenConquer.Content.Tool.World;

namespace OpenConquer.Content.Tool.Analysis;

internal readonly record struct ContentDecodeMetrics(int StartupFiles, int Maps, int Frames,
    TimeSpan StartupTime, TimeSpan WorldTime);

internal static class ContentDecodeWorkload
{
    public static (ContentDecodeMetrics Loose, ContentDecodeMetrics Candidate) Compare(
        IClientContentSource loose, IClientContentSource candidate, ContentManifest manifest,
        uint? mapDataId, bool allMaps)
    {
        long start = Stopwatch.GetTimestamp();
        IReadOnlyList<ClientContentRequirement> looseStartup = ClientContentClosure.Resolve(loose);
        TimeSpan looseStartupTime = Stopwatch.GetElapsedTime(start);

        start = Stopwatch.GetTimestamp();
        IReadOnlyList<ClientContentRequirement> candidateStartup = ClientContentClosure.Resolve(candidate);
        TimeSpan candidateStartupTime = Stopwatch.GetElapsedTime(start);

        if (!looseStartup.SequenceEqual(candidateStartup))
        {
            throw new InvalidDataException("Startup content dependencies differ between sources.");
        }

        GameMapCatalog looseCatalog = GameMapCatalog.Load(loose);
        GameMapCatalog candidateCatalog = GameMapCatalog.Load(candidate);
        HashSet<uint> excluded = manifest.MissingMapArchives?
            .Select(missing => missing.MapDataId).ToHashSet() ?? [];

        IEnumerable<uint> selected = allMaps ? looseCatalog.MapDataIds.Order() :
            mapDataId is uint selectedId ? [selectedId] : [];
        TimeSpan looseWorldTime = TimeSpan.Zero;
        TimeSpan candidateWorldTime = TimeSpan.Zero;
        int mapCount = 0;
        int frames = 0;

        foreach (uint id in selected)
        {
            if (excluded.Contains(id))
            {
                continue;
            }

            start = Stopwatch.GetTimestamp();
            GameMapContent original = GameMapContent.Load(loose, looseCatalog, id);
            (WorldMapPuzzleValidationResult puzzle, WorldMapSceneryAniValidationResult scenery,
                WorldMapTerrainSceneValidationResult terrain) = DecodeResources(loose, original);
            looseWorldTime += Stopwatch.GetElapsedTime(start);

            start = Stopwatch.GetTimestamp();
            GameMapContent decoded = GameMapContent.Load(candidate, candidateCatalog, id);
            (WorldMapPuzzleValidationResult candidatePuzzle, WorldMapSceneryAniValidationResult candidateScenery,
                WorldMapTerrainSceneValidationResult candidateTerrain) = DecodeResources(candidate, decoded);
            candidateWorldTime += Stopwatch.GetElapsedTime(start);

            WorldMapSemanticComparer.Compare(original, decoded);
            if (puzzle != candidatePuzzle || scenery != candidateScenery || terrain != candidateTerrain)
            {
                throw new InvalidDataException($"Decoded map resources differ for map data ID {id}.");
            }

            frames = checked(frames + puzzle.FrameCount + scenery.FrameCount + terrain.FrameCount);
            mapCount++;
        }

        return (new ContentDecodeMetrics(looseStartup.Count, mapCount, frames,
                looseStartupTime, looseWorldTime),
            new ContentDecodeMetrics(candidateStartup.Count, mapCount, frames,
                candidateStartupTime, candidateWorldTime));
    }

    private static (WorldMapPuzzleValidationResult Puzzle, WorldMapSceneryAniValidationResult Scenery,
        WorldMapTerrainSceneValidationResult Terrain) DecodeResources(IClientContentSource source,
        GameMapContent map) =>
        (WorldMapPuzzleValidator.Validate(source, map),
            WorldMapSceneryAniValidator.Validate(source, map),
            WorldMapTerrainSceneValidator.Validate(source, map));
}
