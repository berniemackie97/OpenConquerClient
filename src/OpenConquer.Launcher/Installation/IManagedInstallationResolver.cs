namespace OpenConquer.Launcher.Installation;

internal interface IManagedInstallationResolver
{
    Task<ManagedInstallationResolution> ResolveAsync(CancellationToken cancellationToken);
}
