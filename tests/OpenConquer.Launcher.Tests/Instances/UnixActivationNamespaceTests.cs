using System.Diagnostics;
using System.Text;
using OpenConquer.Launcher.Instances;

namespace OpenConquer.Launcher.Tests.Instances;

public sealed class UnixActivationNamespaceTests : IDisposable
{
    private readonly ActivationTestNamespace _namespace = new();
    public void Dispose() => _namespace.Dispose();

    [Fact]
    public void HomeFallback_CreatesPrivateDirectoryUnderProtectedUserOwnedParent()
    {
        if (OperatingSystem.IsWindows())
            return;
        File.SetUnixFileMode(_namespace.Root, (UnixFileMode)0x1ed);
        string pipe = UnixActivationNamespace.Create(_namespace.Root, requirePrivateRoot: false);
        Assert.Equal(Path.Combine(_namespace.Root, ".openconquer-runtime", "activate"), pipe);
        Assert.Equal((UnixFileMode)0x1c0, File.GetUnixFileMode(Path.GetDirectoryName(pipe)!));
        UnixActivationNamespace.ValidateEndpoint(pipe);
    }

    [Fact]
    public async Task PrivateNamespace_CreatesReusableRestrictedEndpoint()
    {
        if (OperatingSystem.IsWindows())
            return;
        string pipe = _namespace.NewPipeName();
        Assert.Equal(pipe, _namespace.NewPipeName());
        Assert.Equal((UnixFileMode)0x1c0, File.GetUnixFileMode(Path.GetDirectoryName(pipe)!));
        await using LauncherActivationServer server = new(pipe);
        _ = server.RunAsync(_ => Task.FromResult(true));
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(5));
        Assert.True(await LauncherActivationProtocol.TryActivateAsync(pipe, deadline.Token));
        Assert.Equal((UnixFileMode)0x180, File.GetUnixFileMode(pipe));
    }

    [Theory]
    [InlineData(0x1ed)] // 0755
    [InlineData(0x1f8)] // 0770
    [InlineData(0x1ff)] // 0777
    public void RuntimeRoot_RejectsIncorrectPermissionsWithoutRepair(int mode)
    {
        if (OperatingSystem.IsWindows())
            return;
        File.SetUnixFileMode(_namespace.Root, (UnixFileMode)mode);
        Assert.Throws<UnauthorizedAccessException>(() => _namespace.NewPipeName());
        Assert.Equal((UnixFileMode)mode, File.GetUnixFileMode(_namespace.Root));
        Assert.False(Directory.Exists(Path.Combine(_namespace.Root, "openconquer")));
    }

    [Fact]
    public void ApplicationDirectory_RejectsInsecureExistingModeWithoutRepair()
    {
        if (OperatingSystem.IsWindows())
            return;
        string pipe = _namespace.NewPipeName();
        string parent = Path.GetDirectoryName(pipe)!;
        File.SetUnixFileMode(parent, (UnixFileMode)0x1ed);
        Assert.Throws<UnauthorizedAccessException>(() => _namespace.NewPipeName());
        Assert.Throws<UnauthorizedAccessException>(() => new LauncherActivationServer(pipe));
        Assert.Equal((UnixFileMode)0x1ed, File.GetUnixFileMode(parent));
    }

    [Fact]
    public void RuntimeRoot_RejectsFinalAndIntermediateSymlinks()
    {
        if (OperatingSystem.IsWindows())
            return;
        string real = Path.Combine(_namespace.Root, "real");
        Directory.CreateDirectory(Path.Combine(real, "child"), (UnixFileMode)0x1c0);
        string link = Path.Combine(_namespace.Root, "link");
        Directory.CreateSymbolicLink(link, real);
        Assert.ThrowsAny<IOException>(() => UnixActivationNamespace.Create(link));
        Assert.ThrowsAny<IOException>(() => UnixActivationNamespace.Create(link + "/child"));
        Assert.False(Directory.Exists(Path.Combine(real, "openconquer")));
    }

    [Fact]
    public void ApplicationDirectory_RejectsSymlinkAndPreservesTarget()
    {
        if (OperatingSystem.IsWindows())
            return;
        string target = Path.Combine(_namespace.Root, "target");
        Directory.CreateDirectory(target, (UnixFileMode)0x1c0);
        Directory.CreateSymbolicLink(Path.Combine(_namespace.Root, "openconquer"), target);
        Assert.ThrowsAny<IOException>(() => _namespace.NewPipeName());
        Assert.Empty(Directory.EnumerateFileSystemEntries(target));
    }

    [Fact]
    public void Endpoint_RejectsRegularFileAndSymlinkWithoutDeletingEither()
    {
        if (OperatingSystem.IsWindows())
            return;
        string pipe = _namespace.NewPipeName();
        File.WriteAllText(pipe, "keep");
        Assert.Throws<UnauthorizedAccessException>(() => new LauncherActivationServer(pipe));
        Assert.Equal("keep", File.ReadAllText(pipe));
        File.Delete(pipe);
        string target = Path.Combine(_namespace.Root, "target");
        File.WriteAllText(target, "target");
        File.CreateSymbolicLink(pipe, target);
        Assert.Throws<UnauthorizedAccessException>(() => new LauncherActivationServer(pipe));
        Assert.Equal(target, new FileInfo(pipe).LinkTarget);
        Assert.Equal("target", File.ReadAllText(target));
    }

    [Theory]
    [InlineData("relative")]
    [InlineData("/tmp/../tmp")]
    [InlineData("/tmp/./x")]
    [InlineData("/tmp/\0x")]
    public void RuntimePath_RejectsUnsafeSyntax(string path)
    {
        if (OperatingSystem.IsWindows())
            return;
        Assert.Throws<ArgumentException>(() => UnixActivationNamespace.Create(path));
    }

    [Fact]
    public async Task SocketLength_IsCheckedAsUtf8BeforeFilesystemMutation()
    {
        if (OperatingSystem.IsWindows())
            return;
        int maximum = OperatingSystem.IsMacOS() ? 103 : 107;
        string suffix = "/openconquer/activate";
        int available = maximum - Encoding.UTF8.GetByteCount(_namespace.Root + "/" + suffix);
        string boundary = _namespace.Root + "/" + new string('a', available);
        Directory.CreateDirectory(boundary, (UnixFileMode)0x1c0);
        string pipe = UnixActivationNamespace.Create(boundary);
        Assert.Equal(maximum, Encoding.UTF8.GetByteCount(pipe));
        await using LauncherActivationServer server = new(pipe);
        _ = server.RunAsync(_ => Task.FromResult(true));
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(5));
        Assert.True(await LauncherActivationProtocol.TryActivateAsync(pipe, deadline.Token));
        string tooLong = boundary + "é";
        Assert.Throws<PathTooLongException>(() => UnixActivationNamespace.Create(tooLong));
        Assert.False(Directory.Exists(tooLong));
    }

    [Fact]
    public void WritableAncestor_IsRejectedEvenWhenRuntimeLeafIsPrivate()
    {
        if (OperatingSystem.IsWindows())
            return;
        string child = Path.Combine(_namespace.Root, "child");
        Directory.CreateDirectory(child, (UnixFileMode)0x1c0);
        File.SetUnixFileMode(_namespace.Root, (UnixFileMode)0x1ff);
        Assert.Throws<UnauthorizedAccessException>(() => UnixActivationNamespace.Create(child));
    }

    [Fact]
    public void MacOs_UsesValidatedOsLocationAndRejectsAclAccessGrants()
    {
        if (!OperatingSystem.IsMacOS())
            return;
        string pipe = UnixActivationNamespace.ForCurrentUser();
        Assert.StartsWith("/private/var/folders/", pipe, StringComparison.Ordinal);
        UnixActivationNamespace.ValidateEndpoint(pipe);

        ProcessStartInfo start = new("/bin/chmod")
        {
            UseShellExecute = false
        };
        start.ArgumentList.Add("+a");
        start.ArgumentList.Add("everyone allow add_file,add_subdirectory,search");
        start.ArgumentList.Add(_namespace.Root);
        using Process process = Process.Start(start)!;
        Assert.True(process.WaitForExit(5000));
        Assert.Equal(0, process.ExitCode);
        Assert.Throws<UnauthorizedAccessException>(() => _namespace.NewPipeName());
    }
}
