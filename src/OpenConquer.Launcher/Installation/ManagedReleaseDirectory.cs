namespace OpenConquer.Launcher.Installation;

/// <summary>Validates the closed set of entries owned by one managed release generation.</summary>
internal static class ManagedReleaseDirectory
{
    public static bool HasExpectedShape(string releaseRoot)
    {
        HashSet<string> expected = new(StringComparer.Ordinal)
        {
            ManagedInstallationManifest.ExpectedClientRoot,
            ManagedReleaseManifest.FileName,
            ManagedReleaseSignature.FileName,
        };

        foreach (FileSystemInfo entry in new DirectoryInfo(releaseRoot).EnumerateFileSystemInfos())
        {
            entry.Refresh();
            if (entry.LinkTarget is not null || (entry.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new LinkedInstallationPathException();
            }

            if (!expected.Remove(entry.Name))
            {
                return false;
            }
        }

        return expected.Count == 0;
    }
}
