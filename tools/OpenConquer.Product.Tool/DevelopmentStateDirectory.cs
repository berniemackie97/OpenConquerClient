namespace OpenConquer.Product.Tool;

/// <summary>Creates and validates the user-scoped state directory used by local development publishing.</summary>
internal static class DevelopmentStateDirectory
{
    private const UnixFileMode DirectoryMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    public static string Prepare(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        string normalizedRootPath = ProductStagingPathGuard.NormalizePath(rootPath, nameof(rootPath));

        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(normalizedRootPath);
        }
        else
        {
            Directory.CreateDirectory(normalizedRootPath, DirectoryMode);
        }

        string validatedRootPath = ProductStagingPathGuard.RequireDirectory(normalizedRootPath, "development state");

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(validatedRootPath, DirectoryMode);

            if (File.GetUnixFileMode(validatedRootPath) != DirectoryMode)
            {
                throw new UnauthorizedAccessException("The development-state directory permissions could not be restricted to the current user.");
            }
        }

        return validatedRootPath;
    }
}
