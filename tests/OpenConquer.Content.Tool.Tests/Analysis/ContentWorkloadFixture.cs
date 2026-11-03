using System.IO.Compression;
using System.Text;
using OpenConquer.Content.Ani;
using OpenConquer.Content.Tool.Tests.World;
using OpenConquer.Tests.Fixtures;

namespace OpenConquer.Content.Tool.Tests.Analysis;

internal static class ContentWorkloadFixture
{
    private static readonly string[] s_terrainNames =
        ["stand01", "stand02", "stand03", "stand05", "stand06", "stand08"];

    public static void WriteNewbieMap(TemporarySourceTree source)
    {
        string fixture = Path.Combine(AppContext.BaseDirectory, "RetailFixture");
        source.WriteBytes("ini/GameMap.dat",
            OpenConquer.Tests.RetailContentFixture.ReadAllBytes(Path.Combine(fixture, "ini", "GameMap.dat")));
        source.WriteBytes("map/map/newbie.7z",
            File.ReadAllBytes(Path.Combine(fixture, "map", "newbie.7z")));
        source.WriteBytes("map/puzzle/newbie.pul",
            File.ReadAllBytes(Path.Combine(fixture, "map", "puzzle", "newbie.pul")));
        source.WriteBytes("map/puzzle/newbiebg.pul",
            File.ReadAllBytes(Path.Combine(fixture, "map", "puzzle", "newbiebg.pul")));

        using (FileStream input = File.OpenRead(Path.Combine(fixture, "ani", "newbie.ani.zlib")))
        using (ZLibStream decompressed = new(input, CompressionMode.Decompress))
        using (MemoryStream output = new())
        {
            decompressed.CopyTo(output);
            source.WriteBytes("ani/newbie.ani", output.ToArray());
        }

        StringBuilder sceneIndex = new(File.ReadAllText(Path.Combine(
            fixture, "ani", "mapscene-newbie-sections.ani")));
        foreach (string name in s_terrainNames)
        {
            source.WriteBytes($"map/Scene/{name}.scene", TerrainSceneTestData.Create(name, 2, 2));
            source.WriteBytes($"data/map/mapobj/sky/{name}.dds", WorldTestDds.CreateDxt1());
            sceneIndex.Append('\n').Append('[').Append(name)
                .Append(".tga]\nFrameAmount=1\nFrame0=data/map/mapobj/sky/")
                .Append(name).Append(".dds\n");
        }

        source.WriteText("ani/MapScene.ani", sceneIndex.ToString());

        ClientContentRoot content = new(source.RootPath);
        foreach (string path in new[] { "ani/newbie.ani", "ani/MapScene.ani" })
        {
            AniIndexFile index = AniIndexFile.Load(content, path, ContentLookupMode.LooseOnly);
            foreach (AniIndexSection section in index.Sections)
            {
                foreach (string frame in section.FramePaths)
                {
                    source.WriteBytes(frame, WorldTestDds.CreateDxt1());
                }
            }
        }
    }
}
