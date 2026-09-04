using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace OpenConquer.Content.Tool.Legacy.ServerDat;

/// <summary>
/// Reads the verified <c>outenserver</c> table from inflated retail <c>Server.dat</c> XML.
/// </summary>
internal static class ServerDatXmlCatalogReader
{
    internal const int MaximumGroupCount = 1024;
    internal const int MaximumServersPerGroup = 100;

    private const long MaximumXmlCharacters = 1024 * 1024;
    private const int RootRowId = 0;
    private const int FirstServerRowId = 0x65;
    private const int ServerRowStride = 0x64;

    private const string TableDataElementName = "table_data";
    private const string TableNameAttributeName = "name";
    private const string OutenserverTableName = "outenserver";

    private const string RowElementName = "row";
    private const string FieldElementName = "field";
    private const string FieldNameAttributeName = "name";

    private const string IdFieldName = "id";
    private const string ChildFieldName = "Child";
    private const string FlashNameFieldName = "FlashName";
    private const string FlashIconFieldName = "FlashIcon";
    private const string FlashHintFieldName = "FlashHint";
    private const string ServerNameFieldName = "ServerName";
    private const string ServerIpFieldName = "ServerIP";
    private const string ServerPortFieldName = "ServerPort";

    public static ServerDatCatalog Read(ReadOnlySpan<byte> xmlPayload)
    {
        if (xmlPayload.IsEmpty)
        {
            throw new InvalidDataException("Server.dat inflated XML payload is empty.");
        }

        XDocument document;

        try
        {
            using MemoryStream stream = new(xmlPayload.ToArray(), writable: false);

            XmlReaderSettings settings = new()
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                MaxCharactersInDocument = MaximumXmlCharacters,
            };

            using XmlReader reader = XmlReader.Create(stream, settings);

            document = XDocument.Load(reader, LoadOptions.None);
        }
        catch (XmlException exception)
        {
            throw new InvalidDataException("Server.dat contains invalid XML.", exception);
        }

        XElement root = document.Root ?? throw new InvalidDataException("Server.dat XML does not contain a document root.");

        XElement[] matchingTables = root.DescendantsAndSelf().Where(static element => element.Name == TableDataElementName
                && string.Equals((string?)element.Attribute(TableNameAttributeName), OutenserverTableName, StringComparison.Ordinal)).Take(2).ToArray();

        if (matchingTables.Length == 0)
        {
            throw new InvalidDataException("Server.dat XML does not contain table_data[name=outenserver].");
        }

        if (matchingTables.Length != 1)
        {
            throw new InvalidDataException("Server.dat XML contains more than one table_data[name=outenserver] table.");
        }

        Dictionary<int, Dictionary<string, string>> rowsById = ReadRows(matchingTables[0]);

        Dictionary<string, string> rootRow = GetRequiredRow(rowsById, RootRowId, "root");

        int groupCount = ParseCount(rootRow, ChildFieldName, MaximumGroupCount, "root group");

        List<ServerDatGroup> groups = new(groupCount);

        for (int groupIndex = 1; groupIndex <= groupCount; groupIndex++)
        {
            Dictionary<string, string> groupRow = GetRequiredRow(rowsById, groupIndex, $"group {groupIndex}");

            int serverCount = ParseCount(groupRow, ChildFieldName, MaximumServersPerGroup, $"group {groupIndex} server");

            int firstServerId = checked(FirstServerRowId + (groupIndex - 1) * ServerRowStride);

            List<ServerDatServer> servers = new(serverCount);

            for (int serverOffset = 0; serverOffset < serverCount; serverOffset++)
            {
                int serverId = checked(firstServerId + serverOffset);

                Dictionary<string, string> serverRow = GetRequiredRow(rowsById, serverId, $"server {serverId}");

                servers.Add(new ServerDatServer(serverId, GetRequiredField(serverRow, FlashNameFieldName, $"server {serverId}"),
                    NormalizeFlashIcon(GetOptionalField(serverRow, FlashIconFieldName)), GetOptionalField(serverRow, FlashHintFieldName) ?? string.Empty,
                    GetRequiredField(serverRow, ServerNameFieldName, $"server {serverId}"), GetRequiredField(serverRow, ServerIpFieldName, $"server {serverId}"),
                    GetRequiredField(serverRow, ServerPortFieldName, $"server {serverId}")));
            }

            groups.Add(new ServerDatGroup(groupIndex, GetRequiredField(groupRow, FlashNameFieldName, $"group {groupIndex}"),
                NormalizeFlashIcon(GetOptionalField(groupRow, FlashIconFieldName)), servers));
        }

        return new ServerDatCatalog(groups);
    }

    private static Dictionary<int, Dictionary<string, string>> ReadRows(XElement table)
    {
        Dictionary<int, Dictionary<string, string>> rowsById = [];

        foreach (XElement rowElement in table.Elements(RowElementName))
        {
            Dictionary<string, string> fields = new(StringComparer.Ordinal);

            foreach (XElement fieldElement in rowElement.Elements(FieldElementName))
            {
                string? fieldName = (string?)fieldElement.Attribute(FieldNameAttributeName);

                if (string.IsNullOrWhiteSpace(fieldName))
                {
                    throw new InvalidDataException("Server.dat outenserver row contains a field without a valid name.");
                }

                if (!fields.TryAdd(fieldName, fieldElement.Value))
                {
                    throw new InvalidDataException($"Server.dat outenserver row contains duplicate field '{fieldName}'.");
                }
            }

            string idValue = GetRequiredField(fields, IdFieldName, "outenserver row");

            if (!int.TryParse(idValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) || id < 0)
            {
                throw new InvalidDataException($"Server.dat outenserver row contains invalid id '{idValue}'.");
            }

            if (!rowsById.TryAdd(id, fields))
            {
                throw new InvalidDataException($"Server.dat outenserver table contains duplicate row id {id}.");
            }
        }

        return rowsById;
    }

    private static Dictionary<string, string> GetRequiredRow(Dictionary<int, Dictionary<string, string>> rowsById, int id, string description)
    {
        if (rowsById.TryGetValue(id, out Dictionary<string, string>? row))
        {
            return row;
        }

        throw new InvalidDataException($"Server.dat outenserver table does not define required {description} row id {id}.");
    }

    private static string GetRequiredField(Dictionary<string, string> fields, string fieldName, string rowDescription)
    {
        if (fields.TryGetValue(fieldName, out string? value) && value.Length != 0)
        {
            return value;
        }

        throw new InvalidDataException($"Server.dat {rowDescription} does not define required field '{fieldName}'.");
    }

    private static string? GetOptionalField(Dictionary<string, string> fields, string fieldName)
    {
        return fields.TryGetValue(fieldName, out string? value) ? value : null;
    }

    private static int ParseCount(Dictionary<string, string> fields, string fieldName, int maximum, string description)
    {
        string value = GetRequiredField(fields, fieldName, description);

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count) || count < 0 || count > maximum)
        {
            throw new InvalidDataException($"Server.dat {description} count '{value}' is outside the supported range " + $"0 through {maximum}.");
        }

        return count;
    }

    private static string? NormalizeFlashIcon(string? value)
    {
        if (value is null)
        {
            return null;
        }

        return string.Equals(value, "NULL", StringComparison.OrdinalIgnoreCase) ? null : value;
    }
}
