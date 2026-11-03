using System.Buffers.Binary;
using System.Text;
using OpenConquer.Content.Mesh;

namespace OpenConquer.Content.Tests.Mesh;

public sealed class C3RoleCatalogTests
{
    [Fact]
    public void ReadsEncryptedDirectoryAndRetainsReferencedPayload()
    {
        byte[] data = Convert.FromHexString(
            "42444d476c000000180000000100000052534442000000005f360cebcabe18c975f0e7985ac8409e" +
            "da3fe97eb19253c2324868b6c2bad7be261771c93fbc1691bf45b1d09d5a3e721596dc822928" +
            "cbffbc4c26ce40e226ca2462c3ddc6528d5506071cfb1aac9a8d22df7961");
        C3DatabaseArchive archive = C3DatabaseArchive.Parse(data);

        Assert.Equal("RSDB", Encoding.ASCII.GetString(archive.ReadRequired("ini/3DObj.dbc").Span[..4]));
        Assert.Throws<FileNotFoundException>(() => archive.ReadRequired("ini/missing.dbc"));

        data[0] ^= 1;
        Assert.Throws<InvalidDataException>(() => C3DatabaseArchive.Parse(data));
    }

    [Fact]
    public void ResourcePathTableUsesLatestDuplicateIdentifier()
    {
        byte[] data = new byte[28 + 14];
        BinaryPrimitives.WriteUInt32LittleEndian(data, 0x42445352);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(8), 2000000);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(12), 24);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(16), 2000000);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(20), 28);
        "old"u8.CopyTo(data.AsSpan(24));
        "new"u8.CopyTo(data.AsSpan(28));

        Dictionary<uint, string> paths = C3ResourcePathTable.Read(data);

        Assert.Equal("new", paths[2000000]);
    }

    [Fact]
    public void DualKeyIndexUsesBothKeyHalvesAndRetainsLatestDuplicate()
    {
        byte[] data = new byte[44 + 12];
        BinaryPrimitives.WriteUInt32LittleEndian(data, 0x42445352);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), 3);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(8), 100);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(12), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(16), 44);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(20), 100);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(24), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(28), 48);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(32), 100);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(36), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(40), 52);
        "old"u8.CopyTo(data.AsSpan(44));
        "alt"u8.CopyTo(data.AsSpan(48));
        "new"u8.CopyTo(data.AsSpan(52));

        Dictionary<ulong, string> paths = C3ResourcePathTable.ReadDualKey(data);
        Assert.Equal("new", paths[(1UL << 32) | 100]);
        Assert.Equal("alt", paths[(2UL << 32) | 100]);

        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(40), 43);
        Assert.Throws<InvalidDataException>(() => C3ResourcePathTable.ReadDualKey(data));
    }

    [Fact]
    public void ReadsRoleConfigurationAndPartDescriptor()
    {
        byte[] config = new byte[12 + 0x220 + 0x24];
        BinaryPrimitives.WriteUInt32LittleEndian(config, 0x54504F52);
        BinaryPrimitives.WriteUInt32LittleEndian(config.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(config.AsSpan(8), 1);
        "body"u8.CopyTo(config.AsSpan(12));
        "ini/armor.dbc"u8.CopyTo(config.AsSpan(12 + 0x20));
        "ini/3dmotion.dbc"u8.CopyTo(config.AsSpan(12 + 0x120));
        "v_armet"u8.CopyTo(config.AsSpan(12 + 0x220 + 4));

        C3RolePartTables roles = C3RolePartTables.Read(config);
        Assert.Equal("ini/armor.dbc", roles.Sources["body"].MeshDatabase);
        Assert.Equal("ini/3dmotion.dbc", roles.Sources["body"].MotionDatabase);
        Assert.Contains("v_armet", roles.VirtualMeshNames);
        Assert.Contains("V_ARMET", roles.VirtualMeshNames);

        byte[] meshes = new byte[8 + 8 + 16];
        BinaryPrimitives.WriteUInt32LittleEndian(meshes, 0x4853454D);
        BinaryPrimitives.WriteUInt32LittleEndian(meshes.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(meshes.AsSpan(8), 2_000_000);
        BinaryPrimitives.WriteUInt32LittleEndian(meshes.AsSpan(12), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(meshes.AsSpan(16), 2_000_000);
        BinaryPrimitives.WriteUInt32LittleEndian(meshes.AsSpan(20), 2_000_000);
        meshes[29] = 5;
        meshes[30] = 6;

        C3PartDescriptor descriptor = Assert.Single(C3RolePartTables.ReadMeshes(meshes)[2_000_000]);
        Assert.Equal(2_000_000u, descriptor.ObjectId);
        Assert.Equal(2_000_000u, descriptor.PrimaryTextureId);
        Assert.Equal((byte)5, descriptor.SourceBlend);
        Assert.Equal((byte)6, descriptor.DestinationBlend);
    }
}
