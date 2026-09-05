namespace OpenConquer.Launcher.Installation;

/// <summary>Local identity/layout evidence, never an integrity certificate or launch grant.</summary>
internal abstract record InstallationInspection
{
    private InstallationInspection()
    {
    }

    internal sealed record Located(Version AssemblyVersion) : InstallationInspection;
    internal sealed record Rejected(InstallationIssue Issue) : InstallationInspection;
}
