namespace OpenConquer.Content.Tool.CommandLine;

internal abstract record ContentToolCommand;

internal sealed record ImportContentSetCommand(string SourceRootPath, string DestinationRootPath) : ContentToolCommand;
internal sealed record ValidateStartupCommand(string ContentRootPath) : ContentToolCommand;
internal sealed record VerifyContentSetCommand(string ContentSetRootPath) : ContentToolCommand;
internal sealed record InspectServerDatCommand(string FilePath) : ContentToolCommand;
