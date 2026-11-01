using OpenConquer.Content.Tool.CommandLine;
using OpenConquer.Content.Tool.Import;
using OpenConquer.Content.Tool.Legacy.ServerDat;
using OpenConquer.Content.Tool.Manifest;
using OpenConquer.Content.Tool.Startup;
using OpenConquer.Content.Tool.Verify;
using OpenConquer.Content.Tool.World;

namespace OpenConquer.Content.Tool;

internal static class Program
{
    private const int SuccessExitCode = 0;
    private const int InvalidArgumentsExitCode = 2;
    private const int OperationFailedExitCode = 1;

    private static int Main(string[] args)
    {
        if (!ContentToolCommandLine.TryParse(args, Environment.CurrentDirectory, out ContentToolCommand? command, out string? errorMessage))
        {
            Console.Error.WriteLine($"OpenConquer.Content.Tool: {errorMessage}");

            foreach (string usageLine in ContentToolCommandLine.UsageLines)
            {
                Console.Error.WriteLine(usageLine);
            }

            return InvalidArgumentsExitCode;
        }

        try
        {
            Execute(command);

            return SuccessExitCode;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException)
        {
            Console.Error.WriteLine($"OpenConquer.Content.Tool: {exception.Message}");

            return OperationFailedExitCode;
        }
    }

    private static void Execute(ContentToolCommand command)
    {
        switch (command)
        {
            case ImportContentSetCommand import:
                {
                    ContentManifest manifest = ContentSetImporter.Import(import.SourceRootPath, import.DestinationRootPath, import.MapDataId, import.AllMaps);

                    Console.WriteLine($"Imported {manifest.FileCount} file(s) ({manifest.Length} bytes) for {ContentManifest.SourceSetName}.");

                    foreach (ContentManifestEntry entry in manifest.Entries)
                    {
                        Console.WriteLine($"  {entry.SourcePath} ({entry.Length} bytes, {entry.Signature})");
                    }

                    break;
                }

            case ValidateStartupCommand validate:
                {
                    foreach (string line in StartupContentReport.Create(validate.ContentRootPath).ToReportLines())
                    {
                        Console.WriteLine(line);
                    }

                    break;
                }

            case VerifyContentSetCommand verify:
                {
                    ContentManifest manifest = ContentSetVerifier.Verify(verify.ContentSetRootPath, verify.MapDataId, verify.AllMaps);

                    Console.WriteLine($"Verified {manifest.FileCount} file(s) ({manifest.Length} bytes) for {ContentManifest.SourceSetName}.");

                    break;
                }

            case ValidateMapPuzzlesCommand validateMap:
                {
                    WorldMapPuzzleValidationResult result =
                        WorldMapPuzzleValidator.Validate(validateMap.SourceRootPath, validateMap.MapDataId);

                    Console.WriteLine($"Validated MapDataId {validateMap.MapDataId}: {result.PuzzleCount} puzzle bitmaps, " +
                        $"{result.TileAnimationCount} tile animations, {result.FrameCount} decoded frames.");

                    break;
                }

            case ValidateMapSceneryAniCommand validateScenery:
                {
                    WorldMapSceneryAniValidationResult result =
                        WorldMapSceneryAniValidator.Validate(validateScenery.SourceRootPath, validateScenery.MapDataId);

                    Console.WriteLine($"Validated MapDataId {validateScenery.MapDataId}: {result.SceneryCount} ANI-backed scenery records, " +
                        $"{result.AnimationCount} distinct animations, {result.FrameCount} decoded frames.");

                    break;
                }

            case ValidateMapTerrainScenesCommand validateTerrain:
                {
                    WorldMapTerrainSceneValidationResult result =
                        WorldMapTerrainSceneValidator.Validate(validateTerrain.SourceRootPath, validateTerrain.MapDataId);

                    Console.WriteLine($"Validated MapDataId {validateTerrain.MapDataId}: {result.PlacementCount} terrain-scene placements, " +
                        $"{result.SceneFileCount} scene files, {result.ObjectCount} objects, {result.FrameCount} decoded frames.");

                    break;
                }

            case InspectServerDatCommand inspect:
                {
                    ServerDatInspectionReport report = ServerDatInspectionReport.Create(inspect.FilePath);

                    foreach (string line in report.ToReportLines())
                    {
                        Console.WriteLine(line);
                    }

                    break;
                }

            default:
                throw new InvalidOperationException($"Unhandled content-tool command '{command.GetType().Name}'.");
        }
    }
}
