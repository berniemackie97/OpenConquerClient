using OpenConquer.Launcher.Installation;

namespace OpenConquer.Launcher.Tests;

public sealed class ManagedInstallationManifestTests
{
    [Fact]
    public async Task WriteCurrentReplacesDescriptorWhileWindowsReaderIsOpen()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string root = CreateTestRoot();

        try
        {
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, ManagedInstallationManifest.FileName);
            string firstRelease = ManagedReleaseId.Create(1, "first"u8);
            string secondRelease = ManagedReleaseId.Create(2, "second"u8);

            await ManagedInstallationManifest.WriteCurrentAsync(
                path,
                firstRelease,
                fallbackRelease: null,
                CancellationToken.None
            );

            using FileStream reader = new(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read | FileShare.Delete
            );

            await ManagedInstallationManifest.WriteCurrentAsync(
                path,
                secondRelease,
                firstRelease,
                CancellationToken.None
            );

            ManifestReadResult.Accepted result = Assert.IsType<ManifestReadResult.Accepted>(
                await ManagedInstallationManifest.ReadAsync(path, CancellationToken.None)
            );

            Assert.Equal(secondRelease, result.Manifest.ActiveRelease);
            Assert.Equal(firstRelease, result.Manifest.FallbackRelease);
        }
        finally
        {
            TryDeleteTree(root);
        }
    }

    [Fact]
    public async Task WriteCurrentNormalizesUnixDescriptorMode()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string root = CreateTestRoot();

        try
        {
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, ManagedInstallationManifest.FileName);
            string firstRelease = ManagedReleaseId.Create(1, "first"u8);
            string secondRelease = ManagedReleaseId.Create(2, "second"u8);

            await ManagedInstallationManifest.WriteCurrentAsync(
                path,
                firstRelease,
                fallbackRelease: null,
                CancellationToken.None
            );

            File.SetUnixFileMode(path, (UnixFileMode)0x1ff);

            await ManagedInstallationManifest.WriteCurrentAsync(
                path,
                secondRelease,
                firstRelease,
                CancellationToken.None
            );

            UnixFileMode expected =
                UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.GroupRead
                | UnixFileMode.OtherRead;

            Assert.Equal(expected, File.GetUnixFileMode(path));
            Assert.Empty(
                Directory.EnumerateFiles(
                    root,
                    $".{ManagedInstallationManifest.FileName}.*.tmp",
                    SearchOption.TopDirectoryOnly
                )
            );
        }
        finally
        {
            TryDeleteTree(root);
        }
    }

    private static string CreateTestRoot() =>
        Path.Combine(
            Path.GetTempPath(),
            "OpenConquer.Launcher.Tests",
            Guid.NewGuid().ToString("N")
        );

    private static void TryDeleteTree(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (DirectoryNotFoundException) { }
    }
}
