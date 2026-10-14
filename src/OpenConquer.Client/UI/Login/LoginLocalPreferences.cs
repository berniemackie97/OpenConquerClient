using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace OpenConquer.Client.UI.Login;

/// <summary>Bounded, explicitly opted-in account-name history. Passwords are never persisted.</summary>
internal sealed class LoginLocalPreferences
{
    internal const int MaximumAccounts = 10;
    private readonly List<string> _accounts = [];

    public bool MusicEnabled { get; private set; } = true;
    public IReadOnlyList<string> Accounts => _accounts;

    public void SetMusicEnabled(bool enabled) => MusicEnabled = enabled;

    public bool SaveAccount(string account)
    {
        if (!IsValidAccount(account))
            return false;
        _accounts.RemoveAll(value => string.Equals(value, account, StringComparison.Ordinal));
        _accounts.Insert(0, account);
        if (_accounts.Count > MaximumAccounts)
            _accounts.RemoveAt(MaximumAccounts);
        return true;
    }

    public bool RemoveAccount(string account) => _accounts.Remove(account);

    public static bool IsValidAccount(string? account)
    {
        if (account is not { Length: > 0 and <= LoginCredentialInput.MaximumAccountLength })
            return false;
        foreach (char c in account)
            if (c is not (>= '0' and <= '9' or >= 'a' and <= 'z' or >= 'A' and <= 'Z' or '.' or '-' or '@' or '>' or '_'))
                return false;
        return true;
    }

    public static bool TryParse(ReadOnlyMemory<byte> bytes, out LoginLocalPreferences preferences)
    {
        preferences = new LoginLocalPreferences();
        if (bytes.Length is 0 or > LoginLocalPreferencesStore.MaximumDocumentBytes)
            return false;
        try
        {
            using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 4 });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;
            HashSet<string> seen = new(StringComparer.Ordinal);
            foreach (JsonProperty property in root.EnumerateObject())
                if (!seen.Add(property.Name))
                    return false;
            if (seen.Count != 3 || !root.TryGetProperty("schemaVersion", out JsonElement schema) ||
                schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out int version) || version != 1 ||
                !root.TryGetProperty("musicEnabled", out JsonElement music) ||
                music.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                !root.TryGetProperty("accounts", out JsonElement accounts) || accounts.ValueKind != JsonValueKind.Array ||
                accounts.GetArrayLength() > MaximumAccounts)
                return false;
            preferences.MusicEnabled = music.GetBoolean();
            foreach (JsonElement account in accounts.EnumerateArray())
            {
                if (account.ValueKind != JsonValueKind.String || account.GetString() is not { } name ||
                    !IsValidAccount(name) || preferences._accounts.Contains(name, StringComparer.Ordinal))
                    return false;
                preferences._accounts.Add(name);
            }
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public byte[] Serialize()
    {
        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteBoolean("musicEnabled", MusicEnabled);
            writer.WritePropertyName("accounts");
            writer.WriteStartArray();
            foreach (string account in _accounts)
                writer.WriteStringValue(account);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return buffer.ToArray();
    }
}

/// <summary>Private, small atomic preference file. Corruption never causes an implicit overwrite.</summary>
internal sealed partial class LoginLocalPreferencesStore
{
    internal const int MaximumDocumentBytes = 4096;
    private readonly string? _directory;
    private readonly object _gate = new();
    private bool _writable = true;

    public LoginLocalPreferencesStore(string? directory)
    {
        if (directory is not null && !Path.IsPathFullyQualified(directory))
            throw new ArgumentException("Preferences directory must be fully qualified.", nameof(directory));
        _directory = directory;
    }

    public static LoginLocalPreferencesStore ForCurrentUser()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string? root;
        if (OperatingSystem.IsWindows())
            root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        else if (OperatingSystem.IsMacOS())
            root = Path.Combine(home, "Library", "Application Support");
        else if (OperatingSystem.IsLinux())
        {
            string? xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            root = !string.IsNullOrWhiteSpace(xdg) && Path.IsPathFullyQualified(xdg) ? xdg : Path.Combine(home, ".config");
        }
        else
            root = null;
        return new LoginLocalPreferencesStore(!string.IsNullOrEmpty(root) && Path.IsPathFullyQualified(root)
            ? Path.Combine(root, "OpenConquer", "Client") : null);
    }

    public LoginLocalPreferences Load()
    {
        lock (_gate)
        {
            LoginLocalPreferences fallback = new();
            if (_directory is null)
            {
                _writable = false;
                return fallback;
            }
            string path = Path.Combine(_directory, "login-preferences.json");
            try
            {
                if (!File.Exists(path) && !Directory.Exists(path))
                    return fallback;
                if (!IsRegularFile(path))
                {
                    _writable = false;
                    return fallback;
                }
                using FileStream stream = OpenRead(path);
                if (!stream.CanSeek || stream.Length > MaximumDocumentBytes)
                {
                    _writable = false;
                    return fallback;
                }
                byte[] bytes = new byte[checked((int)stream.Length)];
                stream.ReadExactly(bytes);
                if (stream.Length == bytes.Length && LoginLocalPreferences.TryParse(bytes, out LoginLocalPreferences loaded))
                    return loaded;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (PlatformNotSupportedException) { }
            _writable = false;
            return fallback;
        }
    }

    public bool TrySave(LoginLocalPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        lock (_gate)
        {
            if (!_writable || _directory is null)
                return false;
            string? temporary = null;
            try
            {
                Directory.CreateDirectory(_directory);
                if (!IsRegularDirectory(_directory))
                    return false;
                string path = Path.Combine(_directory, "login-preferences.json");
                if ((File.Exists(path) || Directory.Exists(path)) && !IsRegularFile(path))
                    return false;
                byte[] bytes = preferences.Serialize();
                if (bytes.Length > MaximumDocumentBytes)
                    return false;
                temporary = Path.Combine(_directory, ".login-preferences-" + Guid.NewGuid().ToString("N") + ".tmp");
                FileStreamOptions options = new()
                {
                    Mode = FileMode.CreateNew,
                    Access = FileAccess.Write,
                    Share = FileShare.None
                };
                if (!OperatingSystem.IsWindows())
                    options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                using (FileStream stream = new(temporary, options))
                {
                    stream.Write(bytes);
                    stream.Flush(flushToDisk: true);
                }
                File.Move(temporary, path, overwrite: true);
                temporary = null;
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
            finally
            {
                if (temporary is not null)
                {
                    try
                    {
                        File.Delete(temporary);
                    }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }
    }

    private static FileStream OpenRead(string path)
    {
        if (OperatingSystem.IsWindows())
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        if ((!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS()) ||
            RuntimeInformation.ProcessArchitecture is not (Architecture.X64 or Architecture.Arm64))
            throw new PlatformNotSupportedException("Native no-follow preference reads require Linux/macOS x64/arm64.");
        // O_NONBLOCK | O_NOFOLLOW | O_CLOEXEC, as used by the established launcher settings store.
        int flags = OperatingSystem.IsMacOS() ? 0x4 | 0x100 | 0x1000000 : 0x800 | 0x80000 | 0x20000;
        int descriptor = Open(path, flags);
        if (descriptor < 0)
        {
            int error = Marshal.GetLastPInvokeError();
            if (error == 2)
                throw new FileNotFoundException("Login preference file was removed.");
            throw new IOException("Unable to open login preferences.", new Win32Exception(error));
        }
        SafeFileHandle handle = new(descriptor, ownsHandle: true);
        try
        {
            return new FileStream(handle, FileAccess.Read);
        }
        catch { handle.Dispose(); throw; }
    }

    [LibraryImport("libc", EntryPoint = "open", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int Open(string path, int flags);

    private static bool IsRegularFile(string path)
    {
        FileAttributes attributes = File.GetAttributes(path);
        return (attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.Device)) == 0;
    }

    private static bool IsRegularDirectory(string path)
    {
        FileAttributes attributes = File.GetAttributes(path);
        return (attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) == FileAttributes.Directory;
    }
}
