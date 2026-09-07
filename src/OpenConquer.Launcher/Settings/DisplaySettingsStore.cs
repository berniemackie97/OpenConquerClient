using System.Security.Cryptography;

namespace OpenConquer.Launcher.Settings;

/// <summary>Single-launcher preference storage. Data in this file carries no runtime authority.</summary>
internal sealed class DisplaySettingsStore : IDisplaySettingsStore
{
    private readonly string? _directory;
    private readonly object _gate = new();
    private const string FileName = "display.json";

    public DisplaySettingsStore(string? directory)
    {
        if (directory is not null && !Path.IsPathFullyQualified(directory))
        {
            throw new ArgumentException("Settings directory must be absolute.", nameof(directory));
        }

        _directory = directory;
    }

    public Task<SettingsReadResult> LoadAsync(CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            lock (_gate)
            {
                return Read(cancellationToken);
            }
        }, cancellationToken);
    }

    public Task<SettingsIssue?> SaveAsync(DisplayPreferences preferences, string? expectedRevision, bool resetInvalid, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        return Task.Run(() => { lock (_gate) { return Save(preferences, expectedRevision, resetInvalid, cancellationToken); } }, cancellationToken);
    }

    private SettingsReadResult Read(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (_directory is null)
        {
            return new SettingsReadResult.Rejected(SettingsIssue.Unavailable);
        }

        try
        {
            using FileStream stream = SettingsFile.OpenRead(Path.Combine(_directory, FileName));
            if (!stream.CanSeek || stream.Length > DisplaySettingsDocument.MaximumLength)
            {
                return new SettingsReadResult.Rejected(SettingsIssue.Unavailable);
            }

            byte[] bytes = new byte[checked((int)stream.Length)];
            stream.ReadExactly(bytes);
            token.ThrowIfCancellationRequested();
            if (stream.Length != bytes.Length)
            {
                return new SettingsReadResult.Rejected(SettingsIssue.Changed);
            }

            string revision = Convert.ToHexString(SHA256.HashData(bytes));
            return DisplaySettingsDocument.Parse(bytes, revision);
        }
        catch (FileNotFoundException) { return new SettingsReadResult.Loaded(DisplayPreferences.Default, null); }
        catch (DirectoryNotFoundException) { return new SettingsReadResult.Loaded(DisplayPreferences.Default, null); }
        catch (UnauthorizedAccessException) { return new SettingsReadResult.Rejected(SettingsIssue.Unavailable); }
        catch (IOException) { return new SettingsReadResult.Rejected(SettingsIssue.Unavailable); }
    }

    private SettingsIssue? Save(DisplayPreferences preferences, string? expectedRevision, bool resetInvalid, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (_directory is null)
        {
            return SettingsIssue.Unavailable;
        }

        string? temporary = null;
        try
        {
            SettingsReadResult current = Read(token);
            string? revision = current switch
            {
                SettingsReadResult.Loaded loaded => loaded.Revision,
                SettingsReadResult.Rejected rejected => rejected.Revision,
                _ => throw new InvalidOperationException("Invalid settings result.")
            };
            if (current is SettingsReadResult.Rejected failure && failure.Issue != SettingsIssue.Invalid)
            {
                return failure.Issue;
            }

            if (revision != expectedRevision)
            {
                return SettingsIssue.Changed;
            }

            if (current is SettingsReadResult.Rejected && !resetInvalid)
            {
                return SettingsIssue.Invalid;
            }

            token.ThrowIfCancellationRequested();
            Directory.CreateDirectory(_directory);
            temporary = Path.Combine(_directory, ".display-" + Guid.NewGuid().ToString("N") + ".tmp");
            FileStreamOptions options = new()
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None
            };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            }

            using (FileStream stream = new(temporary, options))
            {
                stream.Write(DisplaySettingsDocument.Serialize(preferences));
                stream.Flush(flushToDisk: true);
            }
            token.ThrowIfCancellationRequested();
            File.Move(temporary, Path.Combine(_directory, FileName), overwrite: true);
            temporary = null;
            return null;
        }
        catch (UnauthorizedAccessException) { return SettingsIssue.Unavailable; }
        catch (IOException) { return SettingsIssue.Unavailable; }
        finally
        {
            // Cleanup failures remain observable instead of silently accumulating staged files.
            if (temporary is not null)
            {
                File.Delete(temporary);
            }
        }
    }

    internal static DisplaySettingsStore ForCurrentUser()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string? root = OperatingSystem.IsWindows() ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : OperatingSystem.IsMacOS()
                ? Path.Combine(home, "Library", "Application Support") : OperatingSystem.IsLinux()
                    ? LinuxConfigurationHome(Environment.GetEnvironmentVariable("XDG_CONFIG_HOME"), home) : null;

        return new DisplaySettingsStore(IsAbsolute(root) ? Path.Combine(root!, "OpenConquer", "Launcher") : null);
    }

    internal static string? LinuxConfigurationHome(string? configured, string? home)
    {
        return IsAbsolute(configured) ? configured : IsAbsolute(home) ? Path.Combine(home!, ".config") : null;
    }

    private static bool IsAbsolute(string? path)
    {
        return !string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path) && !path.Contains('\0');
    }
}
