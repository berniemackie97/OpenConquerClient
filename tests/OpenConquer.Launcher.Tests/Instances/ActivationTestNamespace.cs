using OpenConquer.Launcher.Instances;

namespace OpenConquer.Launcher.Tests.Instances;

internal sealed class ActivationTestNamespace : IDisposable
{
    private readonly DirectoryInfo? _directory;

    public ActivationTestNamespace()
    {
        if (!OperatingSystem.IsWindows())
        {
            _directory = Directory.CreateTempSubdirectory("oc");
            File.SetUnixFileMode(_directory.FullName, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    public string Root
    {
        get
        {
            string path = _directory?.FullName ?? throw new PlatformNotSupportedException();
            return OperatingSystem.IsMacOS() && path.StartsWith("/var/", StringComparison.Ordinal) ? "/private" + path : path;
        }
    }

    public string NewPipeName() => OperatingSystem.IsWindows()
        ? "oc-test-" + Guid.NewGuid().ToString("N")
        : UnixActivationNamespace.Create(Root);

    public void Dispose() => _directory?.Delete(recursive: true);
}
