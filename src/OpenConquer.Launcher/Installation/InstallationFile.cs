namespace OpenConquer.Launcher.Installation;

internal static class InstallationFile
{
    public static async Task<byte[]> ReadBoundedAsync(string path, int maximumLength, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumLength);

        FileAttributes attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new LinkedInstallationPathException();
        }

        if ((attributes & (FileAttributes.Directory | FileAttributes.Device)) != 0)
        {
            throw new IOException("The installation entry is not a regular file.");
        }

        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        FileAttributes openedAttributes = File.GetAttributes(stream.SafeFileHandle);
        if ((openedAttributes & (FileAttributes.ReparsePoint | FileAttributes.Directory | FileAttributes.Device)) != 0)
        {
            throw new LinkedInstallationPathException();
        }

        long length = stream.Length;
        if (length is <= 0 || length > maximumLength)
        {
            throw new IOException("The installation file has an invalid length.");
        }

        byte[] bytes = new byte[(int)length];
        await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
        if (stream.Length != length || File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
        {
            throw new IOException("The installation file changed while it was being read.");
        }

        return bytes;
    }
}

internal sealed class LinkedInstallationPathException : IOException;
