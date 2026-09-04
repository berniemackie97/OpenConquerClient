using System.Text;
using OpenConquer.Content.Startup.ServerSelection;
using OpenConquer.Content.Tests.Wdf;

namespace OpenConquer.Content.Tests.Startup.ServerSelection;

public sealed class ServerDatCatalogLoaderTests
{
    [Fact]
    public void Load_ValidLooseServerDat_ReturnsTypedCatalog()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        byte[] encryptedPayload = ServerDatTestEnvelopeBuilder.EncodeXml(
            Encoding.UTF8.GetBytes(
                """
                <mysqldump>
                  <database>
                    <table_data name="outenserver">
                      <row>
                        <field name="id">0</field>
                        <field name="Child">1</field>
                      </row>
                      <row>
                        <field name="id">1</field>
                        <field name="Child">1</field>
                        <field name="FlashName">GroupAlpha</field>
                        <field name="FlashIcon">NULL</field>
                      </row>
                      <row>
                        <field name="id">101</field>
                        <field name="FlashName">ServerOne</field>
                        <field name="FlashIcon">NULL</field>
                        <field name="FlashHint">Stable</field>
                        <field name="ServerName">ServerOne</field>
                        <field name="ServerIP">127.0.0.1</field>
                        <field name="ServerPort">9958</field>
                      </row>
                    </table_data>
                  </database>
                </mysqldump>
                """
            )
        );

        temporaryDirectory.WriteFile(ServerDatCatalogLoader.ContentPath, encryptedPayload);

        PackagedClientContentSource contentSource = PackagedClientContentSource.Open(
            temporaryDirectory.RootPath
        );

        ServerCatalog catalog = ServerDatCatalogLoader.Load(
            contentSource,
            ServerDatTestEnvelopeBuilder.PublicModulus
        );

        ServerGroup group = Assert.Single(catalog.Groups);

        ServerDefinition server = Assert.Single(group.Servers);

        Assert.Equal("GroupAlpha", group.DisplayName);

        Assert.Equal("ServerOne", server.ServerName);

        Assert.Equal("127.0.0.1", server.Host);

        Assert.Equal("9958", server.Port);
    }

    /// <summary>
    /// Native Server.dat loading is a loose-file operation. A package entry with the same virtual
    /// path must not become an implicit fallback when the root file is absent.
    /// </summary>
    [Fact]
    public void Load_WhenServerDatExistsOnlyInPackage_DoesNotUsePackageFallback()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        byte[] encryptedPayload = ServerDatTestEnvelopeBuilder.EncodeXml(
            Encoding.UTF8.GetBytes(
                """
                <mysqldump>
                  <database>
                    <table_data name="outenserver">
                      <row>
                        <field name="id">0</field>
                        <field name="Child">0</field>
                      </row>
                    </table_data>
                  </database>
                </mysqldump>
                """
            )
        );

        temporaryDirectory.WriteFile("ini/package.ini", "server.dat.wdf\n");

        temporaryDirectory.WriteFile(
            "server.dat.wdf",
            WdfTestArchiveBuilder.CreateSingleEntry(
                ServerDatCatalogLoader.ContentPath,
                encryptedPayload
            )
        );

        PackagedClientContentSource contentSource = PackagedClientContentSource.Open(
            temporaryDirectory.RootPath
        );

        Assert.Throws<FileNotFoundException>(() =>
            ServerDatCatalogLoader.Load(contentSource, ServerDatTestEnvelopeBuilder.PublicModulus)
        );
    }

    [Fact]
    public void Load_WhenServerDatExceedsEncryptedSafetyLimit_ThrowsInvalidDataException()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(
            ServerDatCatalogLoader.ContentPath,
            new byte[ServerDatCatalogLoader.MaximumEncryptedFileLength + 1]
        );

        PackagedClientContentSource contentSource = PackagedClientContentSource.Open(
            temporaryDirectory.RootPath
        );

        Assert.Throws<InvalidDataException>(() =>
            ServerDatCatalogLoader.Load(contentSource, ServerDatTestEnvelopeBuilder.PublicModulus)
        );
    }
}
