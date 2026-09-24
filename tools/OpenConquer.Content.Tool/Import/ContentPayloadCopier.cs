using System.Security.Cryptography;

namespace OpenConquer.Content.Tool.Import;

/// <summary>
/// Copies one retail content payload into a content-set tree while fingerprinting it.
/// </summary>
internal static class ContentPayloadCopier
{
    private const int BufferLength = 1024 * 1024;

    public static string CopyAndHash(Stream source, string payloadRootPath, string sourcePath, long expectedLength)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentOutOfRangeException.ThrowIfNegative(expectedLength);

        string destinationPath = Path.Combine(payloadRootPath, ContentPath.ToHostRelativePath(sourcePath));
        string destinationDirectoryPath = Path.GetDirectoryName(destinationPath) ?? throw new InvalidOperationException($"Payload path '{sourcePath}' has no parent directory.");

        Directory.CreateDirectory(destinationDirectoryPath);

        using FileStream destination = new(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferLength, FileOptions.SequentialScan);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        byte[] buffer = new byte[BufferLength];
        long copiedLength = 0;

        while (true)
        {
            int bytesRead = source.Read(buffer);

            if (bytesRead == 0)
            {
                break;
            }

            destination.Write(buffer, 0, bytesRead);
            hash.AppendData(buffer, 0, bytesRead);
            copiedLength = checked(copiedLength + bytesRead);
        }

        if (copiedLength != expectedLength)
        {
            throw new IOException($"Retail content '{sourcePath}' was expected to contain {expectedLength} bytes but {copiedLength} bytes were copied.");
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}
