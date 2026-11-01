using System.IO.Compression;
using OpenConquer.Content.Ani;
using OpenConquer.Content.Tool.World;

namespace OpenConquer.Content.Tool.Tests.World;

public sealed class WorldMapPuzzleValidatorTests
{
    [Fact]
    public void RetailNewbieMap_DecodesEveryReferencedGroundAndBackdropFrame()
    {
        using TemporarySourceTree source = CreateNewbieSource();

        WorldMapPuzzleValidationResult result = WorldMapPuzzleValidator.Validate(source.RootPath, 1010);

        Assert.Equal(2, result.PuzzleCount);
        Assert.Equal(94, result.TileAnimationCount);
        Assert.Equal(94, result.FrameCount);
    }

    [Fact]
    public void MissingReferencedFrame_FailsRatherThanPassingIncompleteContent()
    {
        using TemporarySourceTree source = CreateNewbieSource();
        File.Delete(source.ChildPath("data/map/puzzle/newbie/newbie004.dds"));

        Assert.Throws<FileNotFoundException>(() => WorldMapPuzzleValidator.Validate(source.RootPath, 1010));
    }

    [Fact]
    public void InvalidReferencedFrame_FailsRatherThanPassingIncompleteContent()
    {
        using TemporarySourceTree source = CreateNewbieSource();
        source.WriteBytes("data/map/puzzle/newbie/newbie004.dds", [0x44, 0x44, 0x53]);

        Assert.Throws<InvalidDataException>(() => WorldMapPuzzleValidator.Validate(source.RootPath, 1010));
    }

    [Fact]
    public void MissingCatalog_Fails()
    {
        using TemporarySourceTree source = new();

        Assert.Throws<FileNotFoundException>(() => WorldMapPuzzleValidator.Validate(source.RootPath, 1010));
    }

    private static TemporarySourceTree CreateNewbieSource()
    {
        TemporarySourceTree source = new();
        string fixtureRoot = Path.Combine(AppContext.BaseDirectory, "RetailFixture");

        source.WriteBytes("ini/GameMap.dat", File.ReadAllBytes(Path.Combine(fixtureRoot, "ini", "GameMap.dat")));
        source.WriteBytes("map/map/newbie.7z", File.ReadAllBytes(Path.Combine(fixtureRoot, "map", "newbie.7z")));
        source.WriteBytes("map/puzzle/newbie.pul", File.ReadAllBytes(Path.Combine(fixtureRoot, "map", "puzzle", "newbie.pul")));
        source.WriteBytes("map/puzzle/newbiebg.pul", File.ReadAllBytes(Path.Combine(fixtureRoot, "map", "puzzle", "newbiebg.pul")));

        using (FileStream input = File.OpenRead(Path.Combine(fixtureRoot, "ani", "newbie.ani.zlib")))
        using (ZLibStream decompressed = new(input, CompressionMode.Decompress))
        using (MemoryStream decoded = new())
        {
            decompressed.CopyTo(decoded);
            source.WriteBytes("ani/newbie.ani", decoded.ToArray());
        }

        AniIndexFile index = AniIndexFile.Load(new ClientContentRoot(source.RootPath),
            "ani/newbie.ani", ContentLookupMode.LooseOnly);

        // Images are deliberately minimal test data. Map, puzzle and ANI navigation use unmodified retail files.
        byte[] dds = WorldTestDds.CreateDxt1();
        foreach (AniIndexSection section in index.Sections)
        {
            foreach (string framePath in section.FramePaths)
            {
                source.WriteBytes(framePath, dds);
            }
        }

        return source;
    }

}
