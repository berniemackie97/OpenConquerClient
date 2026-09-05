namespace OpenConquer.Launcher.Installation;

internal interface IInstallationInspector
{
    Task<InstallationInspection> InspectAsync(InstallationRoot root, CancellationToken cancellationToken);
}
