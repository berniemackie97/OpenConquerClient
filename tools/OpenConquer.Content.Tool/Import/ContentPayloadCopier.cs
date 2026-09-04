using System.Security.Cryptography;

namespace OpenConquer.Content.Tool.Import;

/// <summary>
/// Copies one retail file into a content-set payload tree while fingerprinting it.
/// </summary>
internal static class ContentPayloadCopier
{
    private const int BufferLength = 1024 * 1024;

    public static string CopyAndHash(FileInfo sourceFile, string payloadRootPath, string sourcePath, long expectedLength)
    {
        ArgumentNullException.ThrowIfNull(sourceFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadRootPath);

        string destinationPath = Path.Combine(payloadRootPath, ContentPath.ToHostRelativePath(sourcePath));
        string destinationDirectoryPath = Path.GetDirectoryName(destinationPath)
            ?? throw new InvalidOperationException($"Payload path '{sourcePath}' has no parent directory.");

        Directory.CreateDirectory(destinationDirectoryPath);

        using FileStream source = new(sourceFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, BufferLength, FileOptions.SequentialScan);
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
            throw new IOException($"Retail file '{sourcePath}' was {expectedLength} bytes when enumerated but {copiedLength} bytes when copied.");
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}
