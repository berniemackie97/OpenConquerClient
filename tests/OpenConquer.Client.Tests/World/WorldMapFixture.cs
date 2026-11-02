using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using OpenConquer.Content;
using OpenConquer.Content.Map;

namespace OpenConquer.Client.Tests.World;

internal sealed class WorldMapFixture : IDisposable
{
    private readonly string _root;

    public WorldMapFixture(bool twoFrameBackdrop = false, bool missingGround = false)
    {
        _root = Path.Combine(Path.GetTempPath(), "OpenConquer.WorldMapFixture",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        try
        {
            string fixture = Path.Combine(AppContext.BaseDirectory, "RetailMapFixture");
            Write("ini/GameMap.dat", File.ReadAllBytes(Path.Combine(fixture, "ini", "GameMap.dat")));
            Write("map/map/newbie.7z", File.ReadAllBytes(Path.Combine(fixture, "map", "newbie.7z")));
            if (!missingGround)
            {
                Write("map/puzzle/newbie.pul", File.ReadAllBytes(Path.Combine(fixture, "map", "puzzle", "newbie.pul")));
            }
            byte[] backdrop = File.ReadAllBytes(Path.Combine(fixture, "map", "puzzle", "newbiebg.pul"));
            if (twoFrameBackdrop)
            {
                for (int offset = 272; offset < backdrop.Length; offset += sizeof(ushort))
                {
                    BinaryPrimitives.WriteUInt16LittleEndian(backdrop.AsSpan(offset), 50);
                }
            }

            Write("map/puzzle/newbiebg.pul", backdrop);

            using (FileStream compressed = File.OpenRead(Path.Combine(fixture, "ani", "newbie.ani.zlib")))
            using (ZLibStream decoded = new(compressed, CompressionMode.Decompress))
            using (MemoryStream content = new())
            {
                decoded.CopyTo(content);
                byte[] ani = content.ToArray();
                if (twoFrameBackdrop)
                {
                    const string originalSection =
                        "[Puzzle50]\r\nFrameAmount=1\r\nFrame0=data/map/puzzle/newbie/bg000.dds";
                    const string replacementSection =
                        "[Puzzle50]\r\nFrameAmount=2\r\nFrame0=data/map/puzzle/newbie/bg000.dds\r\n" +
                        "Frame1=data/map/puzzle/newbie/bg001.dds";

                    string text = Encoding.Latin1.GetString(ani);
                    if (!text.Contains(originalSection, StringComparison.Ordinal))
                    {
                        throw new InvalidDataException("The retail Puzzle50 fixture has changed.");
                    }

                    ani = Encoding.Latin1.GetBytes(text.Replace(
                        originalSection, replacementSection, StringComparison.Ordinal));
                }

                Write("ani/newbie.ani", ani);
            }

            ClientContentRoot source = new(_root);
            Map = GameMapContent.Load(source, GameMapCatalog.Load(source), 1010);
            Puzzles = GameMapPuzzleResources.Load(source, Map);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public GameMapContent Map
    {
        get;
    }

    public GameMapPuzzleResources Puzzles
    {
        get;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private void Write(string path, byte[] bytes)
    {
        string destination = Path.Combine(_root, path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.WriteAllBytes(destination, bytes);
    }
}
