namespace OpenConquer.Content.Startup.ServerSelection;

/// <summary>
/// Loads the retail startup server catalog from loose <c>Server.dat</c>.
/// </summary>
/// <remarks>
/// Native 5517 consumes <c>Server.dat</c> directly from the client root rather than through WDF
/// package lookup. This boundary therefore deliberately uses <see cref="ContentLookupMode.LooseOnly"/>.
/// Cryptographic envelope decoding and XML schema interpretation remain separate internal stages.
/// </remarks>
public static class ServerDatCatalogLoader
{
    /// <summary>
    /// Retail path of the encrypted startup server catalog.
    /// </summary>
    public const string ContentPath = "Server.dat";

    internal const int MaximumEncryptedFileLength = ServerDatEnvelopeDecoder.EncryptedBlockSize * ServerDatEnvelopeDecoder.MaximumEncryptedBlockCount;

    /// <summary>
    /// Loads and decodes the retail 5517 startup server catalog.
    /// </summary>
    public static ServerCatalog Load(IClientContentSource contentSource)
    {
        return Load(contentSource, ServerDatNativePublicKey.Modulus);
    }

    /// <summary>
    /// Loads a Server.dat-compatible catalog with an explicitly supplied public modulus.
    /// </summary>
    /// <remarks>
    /// This internal overload allows tests to generate independent RSA fixtures while exercising
    /// the complete production content-read, envelope, and XML composition boundary.
    /// </remarks>
    internal static ServerCatalog Load(IClientContentSource contentSource, ReadOnlySpan<byte> publicModulus)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        using Stream stream = contentSource.OpenRequiredRead(ContentPath, ContentLookupMode.LooseOnly);

        byte[] encryptedPayload = ContentRead.ReadBytes(stream, ContentPath, MaximumEncryptedFileLength);
        byte[] xmlPayload = ServerDatEnvelopeDecoder.DecodeToXml(encryptedPayload, publicModulus);

        return ServerDatXmlCatalogReader.Read(xmlPayload);
    }
}
