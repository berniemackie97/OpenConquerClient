using System.Text;
using OpenConquer.Content.Tool.Legacy.ServerDat;

namespace OpenConquer.Content.Tool.Tests.Legacy.ServerDat;

public sealed class ServerDatXmlCatalogReaderTests
{
    [Fact]
    public void Read_ValidOutenserverTable_ReturnsTypedCatalog()
    {
        ServerDatCatalog catalog = Read(
            """
            <mysqldump>
              <database name="conquer">
                <table_data name="outenserver">
                  <row>
                    <field name="id">0</field>
                    <field name="Child">2</field>
                  </row>
                  <row>
                    <field name="id">1</field>
                    <field name="Child">1</field>
                    <field name="FlashName">GroupAlpha</field>
                    <field name="FlashIcon">GroupAlpha.swf</field>
                  </row>
                  <row>
                    <field name="id">2</field>
                    <field name="Child">0</field>
                    <field name="FlashName">GroupBeta</field>
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
        );

        Assert.Equal(2, catalog.Groups.Count);

        ServerDatGroup firstGroup = catalog.Groups[0];

        Assert.Equal(1, firstGroup.Id);
        Assert.Equal("GroupAlpha", firstGroup.FlashName);
        Assert.Equal("GroupAlpha.swf", firstGroup.FlashIcon);

        ServerDatServer server = Assert.Single(firstGroup.Servers);

        Assert.Equal(101, server.Id);
        Assert.Equal("ServerOne", server.FlashName);
        Assert.Null(server.FlashIcon);
        Assert.Equal("Stable", server.FlashHint);
        Assert.Equal("ServerOne", server.ServerName);
        Assert.Equal("127.0.0.1", server.ServerIp);
        Assert.Equal("9958", server.ServerPort);

        ServerDatGroup secondGroup = catalog.Groups[1];

        Assert.Equal(2, secondGroup.Id);
        Assert.Equal("GroupBeta", secondGroup.FlashName);
        Assert.Null(secondGroup.FlashIcon);
        Assert.Empty(secondGroup.Servers);
    }

    [Fact]
    public void Read_WhenOutenserverTableIsMissing_ThrowsInvalidDataException()
    {
        Assert.Throws<InvalidDataException>(() =>
            Read(
                """
                <mysqldump>
                  <database name="conquer">
                    <table_data name="other" />
                  </database>
                </mysqldump>
                """
            )
        );
    }

    [Fact]
    public void Read_WhenOutenserverTableIsDuplicated_ThrowsInvalidDataException()
    {
        Assert.Throws<InvalidDataException>(() =>
            Read(
                """
                <mysqldump>
                  <database name="conquer">
                    <table_data name="outenserver" />
                    <table_data name="outenserver" />
                  </database>
                </mysqldump>
                """
            )
        );
    }

    [Fact]
    public void Read_WhenRowIdIsDuplicated_ThrowsInvalidDataException()
    {
        Assert.Throws<InvalidDataException>(() =>
            Read(
                """
                <mysqldump>
                  <database>
                    <table_data name="outenserver">
                      <row>
                        <field name="id">0</field>
                        <field name="Child">0</field>
                      </row>
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
    }

    [Fact]
    public void Read_WhenRequiredGroupRowIsMissing_ThrowsInvalidDataException()
    {
        Assert.Throws<InvalidDataException>(() =>
            Read(
                """
                <mysqldump>
                  <database>
                    <table_data name="outenserver">
                      <row>
                        <field name="id">0</field>
                        <field name="Child">1</field>
                      </row>
                    </table_data>
                  </database>
                </mysqldump>
                """
            )
        );
    }

    [Fact]
    public void Read_WhenGroupCountExceedsSafetyLimit_ThrowsInvalidDataException()
    {
        Assert.Throws<InvalidDataException>(() =>
            Read(
                $"""
                <mysqldump>
                  <database>
                    <table_data name="outenserver">
                      <row>
                        <field name="id">0</field>
                        <field name="Child">{ServerDatXmlCatalogReader.MaximumGroupCount
                    + 1}</field>
                      </row>
                    </table_data>
                  </database>
                </mysqldump>
                """
            )
        );
    }

    [Fact]
    public void Read_WhenServerCountExceedsNativeRowStride_ThrowsInvalidDataException()
    {
        Assert.Throws<InvalidDataException>(() =>
            Read(
                $"""
                <mysqldump>
                  <database>
                    <table_data name="outenserver">
                      <row>
                        <field name="id">0</field>
                        <field name="Child">1</field>
                      </row>
                      <row>
                        <field name="id">1</field>
                        <field name="Child">{ServerDatXmlCatalogReader.MaximumServersPerGroup
                    + 1}</field>
                        <field name="FlashName">GroupAlpha</field>
                      </row>
                    </table_data>
                  </database>
                </mysqldump>
                """
            )
        );
    }

    [Fact]
    public void Read_WhenRequiredServerFieldIsMissing_ThrowsInvalidDataException()
    {
        Assert.Throws<InvalidDataException>(() =>
            Read(
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
                      </row>
                      <row>
                        <field name="id">101</field>
                        <field name="FlashName">ServerOne</field>
                        <field name="ServerName">ServerOne</field>
                        <field name="ServerIP">127.0.0.1</field>
                      </row>
                    </table_data>
                  </database>
                </mysqldump>
                """
            )
        );
    }

    [Fact]
    public void Read_WhenFieldNameIsDuplicated_ThrowsInvalidDataException()
    {
        Assert.Throws<InvalidDataException>(() =>
            Read(
                """
                <mysqldump>
                  <database>
                    <table_data name="outenserver">
                      <row>
                        <field name="id">0</field>
                        <field name="id">1</field>
                        <field name="Child">0</field>
                      </row>
                    </table_data>
                  </database>
                </mysqldump>
                """
            )
        );
    }

    [Fact]
    public void Read_WhenDocumentContainsDtd_ThrowsInvalidDataException()
    {
        Assert.Throws<InvalidDataException>(() =>
            Read(
                """
                <!DOCTYPE mysqldump [
                  <!ENTITY test "value">
                ]>
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
    }

    private static ServerDatCatalog Read(string xml)
    {
        return ServerDatXmlCatalogReader.Read(Encoding.UTF8.GetBytes(xml));
    }
}
