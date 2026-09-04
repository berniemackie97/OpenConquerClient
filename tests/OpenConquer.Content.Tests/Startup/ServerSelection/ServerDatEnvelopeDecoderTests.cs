using System.Text;
using OpenConquer.Content.Startup.ServerSelection;

namespace OpenConquer.Content.Tests.Startup.ServerSelection;

public sealed class ServerDatEnvelopeDecoderTests
{
    [Fact]
    public void DecodeToXml_ValidEnvelope_InflatesAndParsesCatalog()
    {
        byte[] xmlPayload = Encoding.UTF8.GetBytes(
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
        );

        byte[] encrypted = ServerDatTestEnvelopeBuilder.EncodeXml(xmlPayload);

        byte[] decoded = ServerDatEnvelopeDecoder.DecodeToXml(
            encrypted,
            ServerDatTestEnvelopeBuilder.PublicModulus
        );

        ServerCatalog catalog = ServerDatXmlCatalogReader.Read(decoded);

        ServerGroup group = Assert.Single(catalog.Groups);

        ServerDefinition server = Assert.Single(group.Servers);

        Assert.Equal("GroupAlpha", group.DisplayName);

        Assert.Equal("ServerOne", server.ServerName);

        Assert.Equal("127.0.0.1", server.Host);

        Assert.Equal("9958", server.Port);
    }

    [Fact]
    public void DecodeToXml_ValidEnvelopeAcrossMultipleRsaBlocks_PreservesInflatedPayload()
    {
        byte[] noise = CreateDeterministicNoise(2048);

        string encodedNoise = Convert.ToBase64String(noise);

        byte[] xmlPayload = Encoding.UTF8.GetBytes($"<root><value>{encodedNoise}</value></root>");

        byte[] encrypted = ServerDatTestEnvelopeBuilder.EncodeXml(xmlPayload);

        Assert.True(encrypted.Length > ServerDatEnvelopeDecoder.EncryptedBlockSize);

        byte[] decoded = ServerDatEnvelopeDecoder.DecodeToXml(
            encrypted,
            ServerDatTestEnvelopeBuilder.PublicModulus
        );

        Assert.Equal(xmlPayload, decoded);
    }

    [Fact]
    public void DecodeToXml_EmptyInput_ThrowsInvalidDataException()
    {
        Assert.Throws<InvalidDataException>(() =>
            ServerDatEnvelopeDecoder.DecodeToXml([], ServerDatTestEnvelopeBuilder.PublicModulus)
        );
    }

    [Fact]
    public void DecodeToXml_PartialRsaBlock_ThrowsInvalidDataException()
    {
        Assert.Throws<InvalidDataException>(() =>
            ServerDatEnvelopeDecoder.DecodeToXml(
                new byte[ServerDatEnvelopeDecoder.EncryptedBlockSize - 1],
                ServerDatTestEnvelopeBuilder.PublicModulus
            )
        );
    }

    [Fact]
    public void DecodeToXml_TooManyRsaBlocks_ThrowsInvalidDataException()
    {
        Assert.Throws<InvalidDataException>(() =>
            ServerDatEnvelopeDecoder.DecodeToXml(
                new byte[
                    (ServerDatEnvelopeDecoder.MaximumEncryptedBlockCount + 1)
                        * ServerDatEnvelopeDecoder.EncryptedBlockSize
                ],
                ServerDatTestEnvelopeBuilder.PublicModulus
            )
        );
    }

    [Fact]
    public void DecodeToXml_CiphertextAtModulus_ThrowsInvalidDataException()
    {
        byte[] modulus = ServerDatTestEnvelopeBuilder.PublicModulus;

        Assert.Throws<InvalidDataException>(() =>
            ServerDatEnvelopeDecoder.DecodeToXml(modulus, modulus)
        );
    }

    [Fact]
    public void DecodeToXml_WrongPkcs1BlockType_ThrowsInvalidDataException()
    {
        byte[] block = CreateType1Block([1, 2, 3]);

        block[1] = 0x02;

        byte[] encrypted = ServerDatTestEnvelopeBuilder.EncryptRawBlock(block);

        Assert.Throws<InvalidDataException>(() =>
            ServerDatEnvelopeDecoder.DecodeToXml(
                encrypted,
                ServerDatTestEnvelopeBuilder.PublicModulus
            )
        );
    }

    [Fact]
    public void DecodeToXml_InsufficientType1Padding_ThrowsInvalidDataException()
    {
        byte[] block = new byte[ServerDatEnvelopeDecoder.EncryptedBlockSize];

        block[0] = 0x00;
        block[1] = 0x01;

        block.AsSpan(2, 7).Fill(0xFF);

        block[9] = 0x00;
        block[10] = 0x1F;
        block[11] = 0x8B;
        block[12] = 0x00;

        byte[] encrypted = ServerDatTestEnvelopeBuilder.EncryptRawBlock(block);

        Assert.Throws<InvalidDataException>(() =>
            ServerDatEnvelopeDecoder.DecodeToXml(
                encrypted,
                ServerDatTestEnvelopeBuilder.PublicModulus
            )
        );
    }

    [Fact]
    public void DecodeToXml_MalformedType1Padding_ThrowsInvalidDataException()
    {
        byte[] block = CreateType1Block([0x1F, 0x8B, 0x00]);

        block[5] = 0x7F;

        byte[] encrypted = ServerDatTestEnvelopeBuilder.EncryptRawBlock(block);

        Assert.Throws<InvalidDataException>(() =>
            ServerDatEnvelopeDecoder.DecodeToXml(
                encrypted,
                ServerDatTestEnvelopeBuilder.PublicModulus
            )
        );
    }

    [Fact]
    public void DecodeToXml_DecodedPayloadIsNotGzip_ThrowsInvalidDataException()
    {
        byte[] block = CreateType1Block("not gzip"u8);

        byte[] encrypted = ServerDatTestEnvelopeBuilder.EncryptRawBlock(block);

        Assert.Throws<InvalidDataException>(() =>
            ServerDatEnvelopeDecoder.DecodeToXml(
                encrypted,
                ServerDatTestEnvelopeBuilder.PublicModulus
            )
        );
    }

    [Fact]
    public void DecodeToXml_CorruptGzipPayload_ThrowsInvalidDataException()
    {
        byte[] block = CreateType1Block([0x1F, 0x8B, 0x00, 0x00, 0x00, 0x00]);

        byte[] encrypted = ServerDatTestEnvelopeBuilder.EncryptRawBlock(block);

        Assert.Throws<InvalidDataException>(() =>
            ServerDatEnvelopeDecoder.DecodeToXml(
                encrypted,
                ServerDatTestEnvelopeBuilder.PublicModulus
            )
        );
    }

    [Fact]
    public void DecodeToXml_InflatedPayloadExceedsSafetyLimit_ThrowsInvalidDataException()
    {
        byte[] oversizedPayload = new byte[ServerDatEnvelopeDecoder.MaximumInflatedXmlLength + 1];

        byte[] gzipPayload = ServerDatTestEnvelopeBuilder.Compress(oversizedPayload);

        byte[] encrypted = ServerDatTestEnvelopeBuilder.EncodeGzipPayload(gzipPayload);

        Assert.Throws<InvalidDataException>(() =>
            ServerDatEnvelopeDecoder.DecodeToXml(
                encrypted,
                ServerDatTestEnvelopeBuilder.PublicModulus
            )
        );
    }

    [Fact]
    public void DecodeToXml_InvalidPublicModulusLength_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            ServerDatEnvelopeDecoder.DecodeToXml(
                new byte[ServerDatEnvelopeDecoder.EncryptedBlockSize],
                new byte[ServerDatEnvelopeDecoder.EncryptedBlockSize - 1]
            )
        );
    }

    private static byte[] CreateType1Block(ReadOnlySpan<byte> payload)
    {
        const int paddingLength = 8;

        if (payload.Length > ServerDatEnvelopeDecoder.EncryptedBlockSize - 3 - paddingLength)
        {
            throw new ArgumentException("Test payload is too large.", nameof(payload));
        }

        byte[] block = new byte[ServerDatEnvelopeDecoder.EncryptedBlockSize];

        block[0] = 0x00;
        block[1] = 0x01;

        block.AsSpan(2, paddingLength).Fill(0xFF);

        block[2 + paddingLength] = 0x00;

        payload.CopyTo(block.AsSpan(3 + paddingLength));

        return block;
    }

    private static byte[] CreateDeterministicNoise(int length)
    {
        byte[] bytes = new byte[length];

        uint state = 0xC0FFEE11;

        for (int index = 0; index < bytes.Length; index++)
        {
            state = unchecked(state * 1664525u + 1013904223u);

            bytes[index] = checked((byte)(state >> 24));
        }

        return bytes;
    }
}
