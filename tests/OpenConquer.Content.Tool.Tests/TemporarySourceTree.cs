using System.Buffers.Binary;
using System.Text;

namespace OpenConquer.Content.Tool.Tests;

/// <summary>
/// A disposable directory used to build synthetic source snapshots and content sets.
/// </summary>
internal sealed class TemporarySourceTree : IDisposable
{
    private const uint ProgressBackgroundUid = 0x0561D7F3;
    private const uint MainDialog1Uid = 0xCAE8016F;

    public TemporarySourceTree()
    {
        RootPath = Path.Combine(Path.GetTempPath(), "OpenConquer.Content.Tool.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
    }

    public string RootPath
    {
        get;
    }

    /// <summary>
    /// A path under this tree that has deliberately not been created.
    /// </summary>
    public string ChildPath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        return Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    public void WriteText(string relativePath, string contents)
    {
        ArgumentNullException.ThrowIfNull(contents);
        WriteBytes(relativePath, Encoding.Latin1.GetBytes(contents));
    }

    public void WriteBytes(string relativePath, ReadOnlySpan<byte> contents)
    {
        string filePath = ChildPath(relativePath);
        string directoryPath = Path.GetDirectoryName(filePath) ?? throw new InvalidOperationException($"'{relativePath}' has no parent directory.");

        Directory.CreateDirectory(directoryPath);
        File.WriteAllBytes(filePath, contents);
    }

    /// <summary>
    /// Writes a synthetic retail-shaped source containing every dependency in the implemented runtime content closure.
    /// </summary>
    /// <remarks>
    /// HUD provenance mirrors verified 5517 behavior: Control.ani is loose, ProgressBk.dds and mainDialog1.dds are package-backed,
    /// and MainDialog2.dds is a loose override whose filesystem casing differs from the ANI reference.
    /// Historical Server.dat is deliberately absent because it is not runtime content.
    /// </remarks>
    public void WriteStartupSnapshot(string backgroundFormat = "Data/Main/Logo%d.bmp")
    {
        byte[] progressBackground = CreateSyntheticDds("ProgressBk");
        byte[] mainDialog1 = CreateSyntheticDds("mainDialog1");

        WriteText("version.dat", "5517");
        WriteText("ini/GameSetUp.ini", "[ScreenMode]\nScreenModeRecord=2\n");
        WriteText("ini/info.ini", $"[DlgLogo]\nBgFormat={backgroundFormat}\n");
        WriteText("ini/package.ini", "data.wdf\nc3.wdf\ndata3.wdf\n");
        WriteText("ani/Control.ani", "[Dialog4]\nFrameAmount=2\nFrame0=data/main/mainDialog1.dds\nFrame1=data/main/mainDialog2.dds\n[Progress45]\nFrameAmount=1\nFrame0=data/main/ProgressBk.dds\n");

        WriteBytes("data/main/Logo1.bmp", TestBitmap.CreateTwoByTwo());
        WriteBytes("data/main/Logo2.bmp", TestBitmap.CreateTwoByTwo());
        WriteBytes("data/main/MainDialog2.dds", CreateSyntheticDds("MainDialog2 loose"));
        WriteBytes("data.wdf", CreateWdf((ProgressBackgroundUid, progressBackground), (MainDialog1Uid, mainDialog1)));
    }

    public void Dispose()
    {
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }

    private static byte[] CreateSyntheticDds(string marker)
    {
        return Encoding.ASCII.GetBytes($"DDS {marker}");
    }

    private static byte[] CreateWdf(params (uint Uid, byte[] Payload)[] entries)
    {
        const int headerLength = 12;
        const int entryLength = 16;

        (uint Uid, byte[] Payload)[] orderedEntries = entries.OrderBy(static entry => entry.Uid).ToArray();
        int payloadLength = orderedEntries.Sum(static entry => entry.Payload.Length);
        int tableOffset = checked(headerLength + payloadLength);
        byte[] archive = new byte[checked(tableOffset + orderedEntries.Length * entryLength)];

        BinaryPrimitives.WriteUInt32LittleEndian(archive, 0x57444650);
        BinaryPrimitives.WriteUInt32LittleEndian(archive.AsSpan(4), checked((uint)orderedEntries.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(archive.AsSpan(8), checked((uint)tableOffset));

        int payloadOffset = headerLength;

        for (int index = 0; index < orderedEntries.Length; index++)
        {
            (uint uid, byte[] payload) = orderedEntries[index];
            payload.CopyTo(archive, payloadOffset);

            Span<byte> encodedEntry = archive.AsSpan(tableOffset + index * entryLength, entryLength);
            BinaryPrimitives.WriteUInt32LittleEndian(encodedEntry, uid);
            BinaryPrimitives.WriteUInt32LittleEndian(encodedEntry[4..], checked((uint)payloadOffset));
            BinaryPrimitives.WriteUInt32LittleEndian(encodedEntry[8..], checked((uint)payload.Length));

            payloadOffset = checked(payloadOffset + payload.Length);
        }

        return archive;
    }
}
