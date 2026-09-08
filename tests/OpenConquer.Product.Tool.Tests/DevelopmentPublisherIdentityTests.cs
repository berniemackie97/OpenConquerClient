using System.Security.Cryptography;

namespace OpenConquer.Product.Tool.Tests;

public sealed class DevelopmentPublisherIdentityTests
{
    [Fact]
    public void LoadOrCreateCreatesPersistentP256Identity()
    {
        using TemporaryDirectory temporary = new();

        DevelopmentPublisherIdentityPaths paths = CreatePaths(temporary);

        byte[] firstPublicKey;

        using (
            DevelopmentPublisherIdentity first = DevelopmentPublisherIdentity.LoadOrCreate(paths)
        )
        {
            firstPublicKey = first.ExportPublicKey();
        }

        Assert.True(File.Exists(paths.PrivateKeyPath));

        using DevelopmentPublisherIdentity second = DevelopmentPublisherIdentity.LoadOrCreate(
            paths
        );

        Assert.Equal(firstPublicKey, second.ExportPublicKey());

        using ECDsa publicKey = ECDsa.Create();

        publicKey.ImportSubjectPublicKeyInfo(firstPublicKey, out int bytesRead);

        Assert.Equal(firstPublicKey.Length, bytesRead);

        ECParameters parameters = publicKey.ExportParameters(includePrivateParameters: false);

        Assert.Equal(ECCurve.NamedCurves.nistP256.Oid.Value, parameters.Curve.Oid.Value);
        Assert.Equal(32, parameters.Q.X?.Length);
        Assert.Equal(32, parameters.Q.Y?.Length);
    }

    [Fact]
    public void SignProducesLauncherCompatibleDerSignature()
    {
        using TemporaryDirectory temporary = new();

        DevelopmentPublisherIdentityPaths paths = CreatePaths(temporary);

        using DevelopmentPublisherIdentity identity = DevelopmentPublisherIdentity.LoadOrCreate(
            paths
        );

        byte[] data = "openconquer-local-development-release"u8.ToArray();
        byte[] signature = identity.Sign(data);
        byte[] publicKey = identity.ExportPublicKey();

        using ECDsa verifier = ECDsa.Create();

        verifier.ImportSubjectPublicKeyInfo(publicKey, out int bytesRead);

        Assert.Equal(publicKey.Length, bytesRead);

        Assert.True(
            verifier.VerifyData(
                data,
                signature,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.Rfc3279DerSequence
            )
        );
    }

    [Fact]
    public void LoadOrCreateRejectsMalformedExistingPrivateKeyWithoutReplacingIt()
    {
        using TemporaryDirectory temporary = new();

        DevelopmentPublisherIdentityPaths paths = CreatePaths(temporary);

        Directory.CreateDirectory(paths.RootPath);

        byte[] malformed = [0x01, 0x02, 0x03, 0x04];

        File.WriteAllBytes(paths.PrivateKeyPath, malformed);

        Assert.Throws<InvalidDataException>(() => DevelopmentPublisherIdentity.LoadOrCreate(paths));

        Assert.Equal(malformed, File.ReadAllBytes(paths.PrivateKeyPath));
    }

    [Fact]
    public void LoadOrCreateRejectsNonP256ExistingPrivateKeyWithoutReplacingIt()
    {
        using TemporaryDirectory temporary = new();
        using ECDsa publisher = ECDsa.Create(ECCurve.NamedCurves.nistP384);

        DevelopmentPublisherIdentityPaths paths = CreatePaths(temporary);

        Directory.CreateDirectory(paths.RootPath);

        byte[] privateKey = publisher.ExportPkcs8PrivateKey();

        File.WriteAllBytes(paths.PrivateKeyPath, privateKey);

        Assert.Throws<InvalidDataException>(() => DevelopmentPublisherIdentity.LoadOrCreate(paths));

        Assert.Equal(privateKey, File.ReadAllBytes(paths.PrivateKeyPath));
    }

    [Fact]
    public void LoadOrCreateRejectsPrivateKeyWithTrailingData()
    {
        using TemporaryDirectory temporary = new();
        using ECDsa publisher = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        DevelopmentPublisherIdentityPaths paths = CreatePaths(temporary);

        Directory.CreateDirectory(paths.RootPath);

        byte[] encoded = publisher.ExportPkcs8PrivateKey();
        byte[] withTrailingData = new byte[encoded.Length + 1];

        encoded.CopyTo(withTrailingData, 0);
        withTrailingData[^1] = 0x01;

        File.WriteAllBytes(paths.PrivateKeyPath, withTrailingData);

        Assert.Throws<InvalidDataException>(() => DevelopmentPublisherIdentity.LoadOrCreate(paths));

        Assert.Equal(withTrailingData, File.ReadAllBytes(paths.PrivateKeyPath));
    }

    [Fact]
    public void LoadOrCreateRejectsPrivateKeyOutsideIdentityDirectory()
    {
        using TemporaryDirectory temporary = new();

        string identityRoot = temporary.CreateDirectory("identity");

        DevelopmentPublisherIdentityPaths paths = new(
            identityRoot,
            Path.Combine(temporary.RootPath, "publisher-private.pk8")
        );

        Assert.Throws<InvalidOperationException>(() =>
            DevelopmentPublisherIdentity.LoadOrCreate(paths)
        );

        Assert.False(File.Exists(paths.PrivateKeyPath));
    }

    [Fact]
    public void LoadOrCreateRejectsLinkedPrivateKey()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using TemporaryDirectory temporary = new();
        using ECDsa publisher = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        DevelopmentPublisherIdentityPaths paths = CreatePaths(temporary);

        Directory.CreateDirectory(paths.RootPath);

        string targetPath = Path.Combine(temporary.RootPath, "target.pk8");

        File.WriteAllBytes(targetPath, publisher.ExportPkcs8PrivateKey());
        File.CreateSymbolicLink(paths.PrivateKeyPath, targetPath);

        Assert.Throws<InvalidDataException>(() => DevelopmentPublisherIdentity.LoadOrCreate(paths));
    }

    [Fact]
    public void LoadOrCreateRejectsLinkedIdentityDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using TemporaryDirectory temporary = new();

        string targetRoot = temporary.CreateDirectory("identity-target");
        string linkedRoot = Path.Combine(temporary.RootPath, "identity-link");

        Directory.CreateSymbolicLink(linkedRoot, targetRoot);

        DevelopmentPublisherIdentityPaths paths = new(
            linkedRoot,
            Path.Combine(linkedRoot, DevelopmentPublisherIdentityPaths.PrivateKeyFileName)
        );

        Assert.Throws<InvalidDataException>(() => DevelopmentPublisherIdentity.LoadOrCreate(paths));

        Assert.False(
            File.Exists(
                Path.Combine(targetRoot, DevelopmentPublisherIdentityPaths.PrivateKeyFileName)
            )
        );
    }

    [Fact]
    public void LoadOrCreateEnforcesPrivateUnixPermissions()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using TemporaryDirectory temporary = new();

        DevelopmentPublisherIdentityPaths paths = CreatePaths(temporary);

        using (
            DevelopmentPublisherIdentity identity = DevelopmentPublisherIdentity.LoadOrCreate(paths)
        )
        {
            _ = identity.ExportPublicKey();
        }

        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            File.GetUnixFileMode(paths.RootPath)
        );

        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite,
            File.GetUnixFileMode(paths.PrivateKeyPath)
        );
    }

    [Fact]
    public void LoadOrCreateRepairsExistingUnixPermissions()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using TemporaryDirectory temporary = new();
        using ECDsa publisher = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        DevelopmentPublisherIdentityPaths paths = CreatePaths(temporary);

        Directory.CreateDirectory(paths.RootPath);

        File.WriteAllBytes(paths.PrivateKeyPath, publisher.ExportPkcs8PrivateKey());

        File.SetUnixFileMode(
            paths.RootPath,
            UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead
                | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead
                | UnixFileMode.OtherExecute
        );

        File.SetUnixFileMode(
            paths.PrivateKeyPath,
            UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.GroupRead
                | UnixFileMode.OtherRead
        );

        using DevelopmentPublisherIdentity identity = DevelopmentPublisherIdentity.LoadOrCreate(
            paths
        );

        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            File.GetUnixFileMode(paths.RootPath)
        );

        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite,
            File.GetUnixFileMode(paths.PrivateKeyPath)
        );
    }

    private static DevelopmentPublisherIdentityPaths CreatePaths(TemporaryDirectory temporary)
    {
        string rootPath = Path.Combine(temporary.RootPath, "identity");

        return new DevelopmentPublisherIdentityPaths(
            rootPath,
            Path.Combine(rootPath, DevelopmentPublisherIdentityPaths.PrivateKeyFileName)
        );
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private readonly string _path = Path.Combine(
            Path.GetTempPath(),
            $"openconquer-development-publisher-{Guid.NewGuid():N}"
        );

        public TemporaryDirectory()
        {
            Directory.CreateDirectory(_path);
        }

        public string RootPath => _path;

        public string CreateDirectory(string name)
        {
            string path = Path.Combine(_path, name);

            Directory.CreateDirectory(path);

            return path;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_path, recursive: true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
