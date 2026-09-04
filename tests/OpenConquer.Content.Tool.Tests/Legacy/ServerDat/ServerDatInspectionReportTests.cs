using System.Text;
using System.Text.Json;
using OpenConquer.Content.Tool.Legacy.ServerDat;

namespace OpenConquer.Content.Tool.Tests.Legacy.ServerDat;

public sealed class ServerDatInspectionReportTests
{
    [Fact]
    public void ToReportLines_EscapesSourceValuesAndPreservesHistoricalFields()
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
                        <field name="FlashName">Group&#x0A;Injected</field>
                        <field name="FlashIcon">NULL</field>
                      </row>
                      <row>
                        <field name="id">101</field>
                        <field name="FlashName">Server&quot;One</field>
                        <field name="FlashIcon">NULL</field>
                        <field name="FlashHint">Hint&#x0A;Injected</field>
                        <field name="ServerName">ProtocolServer</field>
                        <field name="ServerIP">127.0.0.1&#x0A;FakeLine</field>
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

        ServerDatInspectionReport report = ServerDatInspectionReport.Create(
            filePath,
            ServerDatTestEnvelopeBuilder.PublicModulus
        );

        string[] lines = report.ToReportLines().ToArray();

        Assert.Equal(5, lines.Length);

        Assert.Equal($"File: {JsonSerializer.Serialize(Path.GetFullPath(filePath))}", lines[0]);

        Assert.Equal("Groups: 1", lines[1]);

        Assert.Equal("Servers: 1", lines[2]);

        Assert.StartsWith("Group 1: ", lines[3], StringComparison.Ordinal);

        Assert.Contains("FlashName=", lines[3], StringComparison.Ordinal);

        Assert.Contains("Injected", lines[3], StringComparison.Ordinal);

        Assert.Contains("FlashIcon=null", lines[3], StringComparison.Ordinal);

        Assert.StartsWith("  Server 101: ", lines[4], StringComparison.Ordinal);

        Assert.Contains("FlashName=", lines[4], StringComparison.Ordinal);

        Assert.Contains("FlashHint=", lines[4], StringComparison.Ordinal);

        Assert.Contains("ServerName=\"ProtocolServer\"", lines[4], StringComparison.Ordinal);

        Assert.Contains("ServerIP=", lines[4], StringComparison.Ordinal);

        Assert.Contains("FakeLine", lines[4], StringComparison.Ordinal);

        Assert.Contains("ServerPort=\"9958\"", lines[4], StringComparison.Ordinal);

        Assert.Contains("FlashIcon=null", lines[4], StringComparison.Ordinal);

        Assert.DoesNotContain(lines, static line => line.Contains('\r') || line.Contains('\n'));
    }
}
