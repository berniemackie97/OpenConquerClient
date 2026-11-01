namespace OpenConquer.Content.Tool.CommandLine;

internal abstract record ContentToolCommand;

internal sealed record ImportContentSetCommand(string SourceRootPath, string DestinationRootPath, uint? MapDataId = null, bool AllMaps = false, bool AvailableMaps = false) : ContentToolCommand;
internal sealed record ValidateStartupCommand(string ContentRootPath) : ContentToolCommand;
internal sealed record VerifyContentSetCommand(string ContentSetRootPath, uint? MapDataId = null, bool AllMaps = false, bool AvailableMaps = false) : ContentToolCommand;
internal sealed record InspectServerDatCommand(string FilePath) : ContentToolCommand;
internal sealed record ValidateMapPuzzlesCommand(string SourceRootPath, uint MapDataId) : ContentToolCommand;
internal sealed record ValidateMapSceneryAniCommand(string SourceRootPath, uint MapDataId) : ContentToolCommand;
internal sealed record ValidateMapTerrainScenesCommand(string SourceRootPath, uint MapDataId) : ContentToolCommand;
