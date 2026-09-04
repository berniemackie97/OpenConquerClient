using System.Security.Cryptography;
using OpenConquer.Content.Startup.ServerSelection;

namespace OpenConquer.Content.Tests.Startup.ServerSelection;

/// <summary>
/// Locks the production Server.dat pipeline to the exact audited retail 5517 fixture.
/// </summary>
public sealed class ServerDatRetailParityTests
{
    private const string ExpectedServerDatSha256 =
        "0B4D366786AA4498C7E470F10FD8BCA716BC1D6CBDA1EB3894666183F8327A90";

    private const string ExpectedInflatedXmlSha256 =
        "5D6B00FF722A8B37AA2981AFFECD478AEE73BDC22CDC498A25B700242B55C35A";

    private const int ExpectedServerDatLength = 2816;
    private const int ExpectedInflatedXmlLength = 38819;
    private const int ExpectedGroupCount = 14;

    [Fact]
    public void RetailFixture_MatchesAuditedBinaryAndDecodedXmlIdentity()
    {
        byte[] encryptedPayload = File.ReadAllBytes(GetFixturePath());

        Assert.Equal(ExpectedServerDatLength, encryptedPayload.Length);

        Assert.Equal(
            ExpectedServerDatSha256,
            Convert.ToHexString(SHA256.HashData(encryptedPayload))
        );

        Assert.Equal(11, encryptedPayload.Length / ServerDatEnvelopeDecoder.EncryptedBlockSize);

        byte[] xmlPayload = ServerDatEnvelopeDecoder.DecodeToXml(encryptedPayload);

        Assert.Equal(ExpectedInflatedXmlLength, xmlPayload.Length);

        Assert.Equal(ExpectedInflatedXmlSha256, Convert.ToHexString(SHA256.HashData(xmlPayload)));
    }

    [Fact]
    public void RetailFixture_LoadsTheVerifiedStartupGroupCatalog()
    {
        string fixturePath = GetFixturePath();

        string contentRoot =
            Path.GetDirectoryName(fixturePath)
            ?? throw new InvalidOperationException(
                "Retail Server.dat fixture does not have a parent directory."
            );

        ServerCatalog catalog = ServerDatCatalogLoader.Load(new ClientContentRoot(contentRoot));

        Assert.Equal(ExpectedGroupCount, catalog.Groups.Count);

        Assert.Equal(
            Enumerable.Range(1, ExpectedGroupCount),
            catalog.Groups.Select(static group => group.Id)
        );
    }

    private static string GetFixturePath()
    {
        return Path.Combine(
            AppContext.BaseDirectory,
            "TestData",
            "retail-5517",
            ServerDatCatalogLoader.ContentPath
        );
    }
}
