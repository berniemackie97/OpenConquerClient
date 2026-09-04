namespace OpenConquer.Content.Tool.Legacy.ServerDat;

/// <summary>
/// Reads and decodes a retail 5517 <c>Server.dat</c> file from an explicit filesystem path.
/// </summary>
internal static class ServerDatFileReader
{
    internal const int MaximumEncryptedFileLength = ServerDatEnvelopeDecoder.EncryptedBlockSize * ServerDatEnvelopeDecoder.MaximumEncryptedBlockCount;

    public static ServerDatCatalog Read(string filePath)
    {
        return Read(filePath, ServerDatNativePublicKey.Modulus);
    }

    internal static ServerDatCatalog Read(string filePath, ReadOnlySpan<byte> publicModulus)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, FileOptions.SequentialScan);

        byte[] encryptedPayload = ReadEncryptedPayload(stream, filePath);
        byte[] xmlPayload = ServerDatEnvelopeDecoder.DecodeToXml(encryptedPayload, publicModulus);

        return ServerDatXmlCatalogReader.Read(xmlPayload);
    }

    private static byte[] ReadEncryptedPayload(Stream stream, string filePath)
    {
        if (stream.Length > MaximumEncryptedFileLength)
        {
            throw new InvalidDataException($"Server.dat file '{filePath}' is {stream.Length} bytes; the limit is {MaximumEncryptedFileLength} bytes.");
        }

        using MemoryStream destination = new(capacity: checked((int)stream.Length));

        byte[] buffer = new byte[4096];
        int totalLength = 0;

        while (true)
        {
            int bytesRead = stream.Read(buffer);

            if (bytesRead == 0)
            {
                return destination.ToArray();
            }

            totalLength = checked(totalLength + bytesRead);

            if (totalLength > MaximumEncryptedFileLength)
            {
                throw new InvalidDataException($"Server.dat file '{filePath}' exceeds the {MaximumEncryptedFileLength}-byte limit.");
            }

            destination.Write(buffer, 0, bytesRead);
        }
    }
}
