using System.Text;
using OpenConquer.Content.Tool.Legacy.ServerDat;

namespace OpenConquer.Content.Tool.Tests.Legacy.ServerDat;

public sealed class ServerDatFileReaderTests
{
    [Fact]
    public void Read_ValidServerDat_ReturnsTypedCatalog()
    {
        using TemporarySourceTree fixture = new();

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

        string filePath = fixture.ChildPath("Server.dat");
        fixture.WriteBytes("Server.dat", encryptedPayload);

        ServerDatCatalog catalog = ServerDatFileReader.Read(
            filePath,
            ServerDatTestEnvelopeBuilder.PublicModulus
        );

        ServerDatGroup group = Assert.Single(catalog.Groups);
        ServerDatServer server = Assert.Single(group.Servers);

        Assert.Equal("GroupAlpha", group.FlashName);
        Assert.Null(group.FlashIcon);

        Assert.Equal("ServerOne", server.FlashName);
        Assert.Null(server.FlashIcon);
        Assert.Equal("Stable", server.FlashHint);
        Assert.Equal("ServerOne", server.ServerName);
        Assert.Equal("127.0.0.1", server.ServerIp);
        Assert.Equal("9958", server.ServerPort);
    }

    [Fact]
    public void Read_WhenServerDatExceedsEncryptedSafetyLimit_ThrowsInvalidDataException()
    {
        using TemporarySourceTree fixture = new();

        string filePath = fixture.ChildPath("Server.dat");

        fixture.WriteBytes(
            "Server.dat",
            new byte[ServerDatFileReader.MaximumEncryptedFileLength + 1]
        );

        Assert.Throws<InvalidDataException>(() =>
            ServerDatFileReader.Read(filePath, ServerDatTestEnvelopeBuilder.PublicModulus)
        );
    }

    [Fact]
    public void Read_WhenFileDoesNotExist_ThrowsFileNotFoundException()
    {
        using TemporarySourceTree fixture = new();

        Assert.Throws<FileNotFoundException>(() =>
            ServerDatFileReader.Read(
                fixture.ChildPath("missing.dat"),
                ServerDatTestEnvelopeBuilder.PublicModulus
            )
        );
    }

    [Fact]
    public void Read_WhenFilePathIsWhitespace_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            ServerDatFileReader.Read(" ", ServerDatTestEnvelopeBuilder.PublicModulus)
        );
    }
}
