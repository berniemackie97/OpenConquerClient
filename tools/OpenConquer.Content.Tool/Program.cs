using OpenConquer.Content.Tool.CommandLine;
using OpenConquer.Content.Tool.Import;
using OpenConquer.Content.Tool.Legacy.ServerDat;
using OpenConquer.Content.Tool.Manifest;
using OpenConquer.Content.Tool.Startup;
using OpenConquer.Content.Tool.Verify;

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
                    ContentManifest manifest = ContentSetImporter.Import(import.SourceRootPath, import.DestinationRootPath);

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
                    ContentManifest manifest = ContentSetVerifier.Verify(verify.ContentSetRootPath);

                    Console.WriteLine($"Verified {manifest.FileCount} file(s) ({manifest.Length} bytes) for {ContentManifest.SourceSetName}.");

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
