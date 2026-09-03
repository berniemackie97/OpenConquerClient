namespace OpenConquer.Content.Tool.CommandLine;

/// <summary>
/// A parsed content-tool invocation. The set is closed: every verb the tool accepts is one of the
/// records below.
/// </summary>
internal abstract record ContentToolCommand;

/// <summary>Imports the resolved retail closure into a new content set.</summary>
/// <param name="SourceRootPath">Authorized retail snapshot to read.</param>
/// <param name="DestinationRootPath">Content-set directory to create.</param>
internal sealed record ImportContentSetCommand(string SourceRootPath, string DestinationRootPath) : ContentToolCommand;

/// <summary>Reports what the startup slice resolves from a content root.</summary>
/// <param name="ContentRootPath">Content root to read.</param>
internal sealed record ValidateStartupCommand(string ContentRootPath) : ContentToolCommand;

/// <summary>Verifies a content set against its manifest.</summary>
/// <param name="ContentSetRootPath">Content-set directory to verify.</param>
internal sealed record VerifyContentSetCommand(string ContentSetRootPath) : ContentToolCommand;
