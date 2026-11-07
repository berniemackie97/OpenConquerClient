using OpenConquer.Networking.Game.Login;

namespace OpenConquer.Client.Startup.LoginFlow;

/// <summary>
/// Reads the files native <c>CSilentInfo</c> loads at construction: <c>ini/Slient.dat</c> for the checksum
/// (<c>CSilentInfo_ReloadDataFile</c>, 0x6BB440) and <c>ini/SlientVersion.ini</c> for the version (0x6BB3AC). The
/// native misspelling is kept. A file that cannot be opened leaves the native default (checksum 0, version 1).
/// </summary>
internal static class SilentInfoFiles
{
    internal static GameSilentInfoReport Read(string contentRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRoot);
        GameSilentInfoReport report = GameSilentInfoReport.WithoutFiles;
        if (TryReadAll(Path.Combine(contentRoot, "ini", "Slient.dat"), int.MaxValue, out byte[] data))
        {
            report = report with
            {
                Checksum = GameSilentInfoReport.ComputeChecksum(data)
            };
        }

        // fread(buffer, 0x80, 1, file): only the first 128 bytes are parsed.
        if (TryReadAll(Path.Combine(contentRoot, "ini", "SlientVersion.ini"), 128, out byte[] version))
        {
            report = report with
            {
                Version = GameSilentInfoReport.ParseVersion(version)
            };
        }

        return report;
    }

    private static bool TryReadAll(string path, int maximumLength, out byte[] bytes)
    {
        try
        {
            using FileStream file = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            int length = (int)Math.Min(file.Length, maximumLength);
            bytes = new byte[length];
            file.ReadExactly(bytes);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            bytes = [];
            return false;
        }
    }
}
