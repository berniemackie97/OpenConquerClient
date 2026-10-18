using System.Text.Json;
using OpenConquer.Client.UI.Login.Credentials;

namespace OpenConquer.Client.UI.Login.Account;

/// <summary>Remembers up to ten account names. Passwords and realm endpoints are never stored here.</summary>
internal sealed class LoginAccountHistory
{
    internal const int Capacity = 10;
    private const int MaximumBytes = 4096;
    private readonly string _directory;
    private readonly string _path;
    private readonly List<string> _names = [];
    private bool _writable = true;

    public IReadOnlyList<string> Accounts => _names;
    public bool Writable => _writable;

    internal LoginAccountHistory(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = Path.GetFullPath(directory);
        _path = Path.Combine(_directory, "login-accounts.json");
        Load();
    }

    public static LoginAccountHistory ForCurrentUser()
    {
        try
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(root))
            {
                throw new InvalidOperationException("A user-local application data directory is required.");
            }

            return new LoginAccountHistory(Path.Combine(root, "OpenConquer", "Client"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            Console.Error.WriteLine("OpenConquer: user-local account history unavailable; continuing without persistence.");
            return new LoginAccountHistory();
        }
    }

    private LoginAccountHistory()
    {
        _directory = _path = string.Empty;
        _writable = false;
    }

    public bool Contains(string name) => _names.Contains(name, StringComparer.Ordinal);

    public bool TryRecord(string name)
    {
        if (!_writable || !IsValidName(name))
        {
            return false;
        }

        // The original itemBtn/addItem Flash list appends only names not already present.
        if (_names.Contains(name, StringComparer.Ordinal))
        {
            return true;
        }

        List<string> next = new(_names);
        if (next.Count == Capacity)
        {
            next.RemoveAt(0);
        }

        next.Add(name);
        return Commit(next);
    }

    public bool TryRemove(string name)
    {
        if (!_writable || !IsValidName(name) || !_names.Contains(name, StringComparer.Ordinal))
        {
            return false;
        }

        List<string> next = new(_names);
        next.Remove(name);
        return Commit(next);
    }

    internal static bool IsValidName(string? name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > LoginCredentialInput.MaximumAccountLength)
        {
            return false;
        }

        foreach (char character in name)
        {
            if (!IsAccountCharacter(character))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The characters an account name may contain; passwords are limited to the same set.</summary>
    internal static bool IsAccountCharacter(char value) =>
        value is >= '0' and <= '9' or >= 'a' and <= 'z' or >= 'A' and <= 'Z' or '.' or '-' or '@' or '>' or '_';

    private void Load()
    {
        try
        {
            if (Directory.Exists(_directory) && (File.GetAttributes(_directory) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("The account history directory cannot be a symbolic link.");
            }

            if (!File.Exists(_path))
            {
                // Broken symbolic links are not treated as missing documents.
                if (Path.Exists(_path) || new FileInfo(_path).LinkTarget is not null)
                {
                    _writable = false;
                }

                return;
            }
            if ((File.GetAttributes(_path) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("The account history path cannot be a symbolic link.");
            }

            using FileStream file = new(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (file.Length is <= 0 or > MaximumBytes)
            {
                throw new InvalidDataException("Invalid account history size.");
            }

            byte[] bytes = new byte[(int)file.Length];
            file.ReadExactly(bytes);
            using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 3 });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 2 ||
                !root.TryGetProperty("schemaVersion", out JsonElement version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out int schemaVersion) || schemaVersion != 1 ||
                !root.TryGetProperty("accounts", out JsonElement accounts) || accounts.ValueKind != JsonValueKind.Array || accounts.GetArrayLength() > Capacity)
            {
                throw new InvalidDataException("Unsupported account history document.");
            }

            HashSet<string> unique = new(StringComparer.Ordinal);
            foreach (JsonElement element in accounts.EnumerateArray())
            {
                string? name = element.ValueKind == JsonValueKind.String ? element.GetString() : null;
                if (!IsValidName(name) || !unique.Add(name!))
                {
                    throw new InvalidDataException("Invalid account history entry.");
                }

                _names.Add(name!);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or ArgumentException)
        {
            _names.Clear();
            _writable = false;
            Console.Error.WriteLine("OpenConquer: local account history could not be validated; updates disabled.");
        }
    }

    private bool Commit(List<string> next)
    {
        string? temp = null;
        try
        {
            if (Directory.Exists(_directory) && (File.GetAttributes(_directory) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("The account history directory cannot be a symbolic link.");
            }

            Directory.CreateDirectory(_directory);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(_directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            if (Path.Exists(_path) && (File.GetAttributes(_path) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("The account history path cannot be a symbolic link.");
            }

            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = 1,
                accounts = next
            });
            if (bytes.Length > MaximumBytes)
            {
                throw new InvalidDataException("Account history is too large.");
            }

            temp = Path.Combine(_directory, $".login-accounts-{Guid.NewGuid():N}.tmp");
            FileStreamOptions options = new()
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.WriteThrough,
            };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            }

            using (FileStream stream = new(temp, options))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temp, _path, overwrite: true);
            temp = null;
            _names.Clear();
            _names.AddRange(next);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            _writable = false;
            Console.Error.WriteLine("OpenConquer: local account history could not be saved; updates disabled.");
            return false;
        }
        finally
        {
            if (temp is not null)
            {
                try
                {
                    File.Delete(temp);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // A leftover temporary file is harmless; the history file itself is intact.
                }
            }
        }
    }
}
