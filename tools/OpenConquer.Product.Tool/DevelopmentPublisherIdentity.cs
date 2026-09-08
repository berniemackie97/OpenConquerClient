using System.Security.Cryptography;

namespace OpenConquer.Product.Tool;

/// <summary>Persistent development-only publisher used to authenticate locally composed products.</summary>
internal sealed class DevelopmentPublisherIdentity : IDisposable
{
    private const int MaximumPrivateKeyLength = 1024;

    private const UnixFileMode DirectoryMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode PrivateKeyMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private readonly ECDsa _algorithm;

    private DevelopmentPublisherIdentity(ECDsa algorithm)
    {
        _algorithm = algorithm;
    }

    public static DevelopmentPublisherIdentity LoadOrCreate(DevelopmentPublisherIdentityPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        string rootPath = ProductStagingPathGuard.NormalizePath(paths.RootPath, nameof(paths.RootPath));
        string privateKeyPath = ProductStagingPathGuard.NormalizePath(paths.PrivateKeyPath, nameof(paths.PrivateKeyPath));

        if (!string.Equals(Path.GetDirectoryName(privateKeyPath), rootPath, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The development publisher private key must be stored directly in its identity directory.");
        }

        PrepareIdentityDirectory(rootPath);

        try
        {
            return LoadExisting(privateKeyPath);
        }
        catch (FileNotFoundException)
        {
            CreatePrivateKey(privateKeyPath);
            return LoadExisting(privateKeyPath);
        }
    }

    public byte[] ExportPublicKey()
    {
        return _algorithm.ExportSubjectPublicKeyInfo();
    }

    public byte[] Sign(ReadOnlySpan<byte> data)
    {
        return _algorithm.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
    }

    public void Dispose()
    {
        _algorithm.Dispose();
    }

    private static void PrepareIdentityDirectory(string rootPath)
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(rootPath);
        }
        else
        {
            Directory.CreateDirectory(rootPath, DirectoryMode);
        }

        _ = ProductStagingPathGuard.RequireDirectory(rootPath, "development publisher identity");

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(rootPath, DirectoryMode);

            if (File.GetUnixFileMode(rootPath) != DirectoryMode)
            {
                throw new UnauthorizedAccessException("The development publisher identity directory permissions could not be restricted to the current user.");
            }
        }
    }

    private static DevelopmentPublisherIdentity LoadExisting(string privateKeyPath)
    {
        string path = ProductStagingPathGuard.RequireRegularFile(privateKeyPath, "development publisher private key");

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, PrivateKeyMode);

            if (File.GetUnixFileMode(path) != PrivateKeyMode)
            {
                throw new UnauthorizedAccessException("The development publisher private-key permissions could not be restricted to the current user.");
            }
        }

        byte[] privateKey = ReadPrivateKey(path);

        try
        {
            ECDsa algorithm = ECDsa.Create();

            try
            {
                algorithm.ImportPkcs8PrivateKey(privateKey, out int bytesRead);

                if (bytesRead != privateKey.Length)
                {
                    throw new InvalidDataException("The development publisher private key contains trailing data.");
                }

                ValidatePrivateKey(algorithm);

                return new DevelopmentPublisherIdentity(algorithm);
            }
            catch
            {
                algorithm.Dispose();
                throw;
            }
        }
        catch (CryptographicException exception)
        {
            throw new InvalidDataException("The development publisher private key is invalid.", exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateKey);
        }
    }

    private static void CreatePrivateKey(string privateKeyPath)
    {
        using ECDsa algorithm = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        byte[] privateKey = algorithm.ExportPkcs8PrivateKey();
        string temporaryPath = privateKeyPath + $".tmp-{Guid.NewGuid():N}";
        bool completed = false;

        try
        {
            FileStreamOptions options = new()
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
            };

            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = PrivateKeyMode;
            }

            using (FileStream stream = new(temporaryPath, options))
            {
                stream.Write(privateKey);
                stream.Flush(flushToDisk: true);
            }

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(temporaryPath, PrivateKeyMode);

                if (File.GetUnixFileMode(temporaryPath) != PrivateKeyMode)
                {
                    throw new UnauthorizedAccessException("The development publisher private-key permissions could not be restricted to the current user.");
                }
            }

            File.Move(temporaryPath, privateKeyPath);
            completed = true;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateKey);

            if (!completed)
            {
                TryDelete(temporaryPath);
            }
        }
    }

    private static byte[] ReadPrivateKey(string path)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.None);

        if (stream.Length is <= 0 or > MaximumPrivateKeyLength)
        {
            throw new InvalidDataException("The development publisher private-key length is invalid.");
        }

        byte[] bytes = new byte[(int)stream.Length];

        stream.ReadExactly(bytes);

        if (stream.Length != bytes.Length)
        {
            CryptographicOperations.ZeroMemory(bytes);
            throw new IOException("The development publisher private key changed while it was being read.");
        }

        return bytes;
    }

    private static void ValidatePrivateKey(ECDsa algorithm)
    {
        ECParameters parameters = algorithm.ExportParameters(includePrivateParameters: true);

        try
        {
            if (parameters.Curve.Oid.Value != ECCurve.NamedCurves.nistP256.Oid.Value || parameters.Q.X is not { Length: 32 }
                || parameters.Q.Y is not { Length: 32 } || parameters.D is not { Length: 32 })
            {
                throw new InvalidDataException("Only ECDSA P-256 development publisher private keys are supported.");
            }
        }
        finally
        {
            if (parameters.D is not null)
            {
                CryptographicOperations.ZeroMemory(parameters.D);
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
