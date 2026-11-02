using OpenConquer.Content.Map;
using OpenConquer.Content.Tool.Analysis;
using OpenConquer.Content.Tool.Import;

namespace OpenConquer.Content.Tool.Tests.Import;

public sealed class MapAssetCompilerTests
{
    [Fact]
    public void CompilesMap1010ToDeterministicEngineData()
    {
        using TemporarySourceTree source = new();
        source.WriteStartupSnapshot();
        ContentWorkloadFixture.WriteNewbieMap(source);
        PackagedClientContentSource content = PackagedClientContentSource.Open(source.RootPath);

        MapAsset map = MapAssetCompiler.Compile(content, 1010);
        byte[] first = MapAssetCodec.Encode(map);
        byte[] repeat = MapAssetCodec.Encode(MapAssetCompiler.Compile(content, 1010));
        MapAsset decoded = MapAssetCodec.Decode(first);

        Assert.Equal(first, repeat);
        Assert.Equal(1010u, decoded.MapDataId);
        Assert.Equal((132, 132), (decoded.Width, decoded.Height));
        Assert.Equal(132 * 132, decoded.Cells.Count);
        Assert.Equal(20, decoded.Objects.Count(x => x is MapTerrainObject));
        Assert.Equal(16, decoded.Objects.Count(x => x is MapAnimatedObject));
        Assert.Equal("map/puzzle/newbie", decoded.GroundPuzzleAssetId);
        MapLayer layer = Assert.Single(decoded.Layers);
        Assert.Equal((18, 35), (layer.ParallaxXPercent, layer.ParallaxYPercent));
        Assert.IsType<MapPuzzleLayerEntry>(Assert.Single(layer.Entries));
        Assert.Equal(first, MapAssetCodec.Encode(decoded));
    }

    [Fact]
    public void CompileToFileDoesNotOverwriteExistingAsset()
    {
        using TemporarySourceTree source = new();
        using TemporarySourceTree output = new();
        source.WriteStartupSnapshot();
        ContentWorkloadFixture.WriteNewbieMap(source);

        string destination = output.ChildPath("1010.ocmap");
        MapAssetCompiler.Compile(source.RootPath, 1010, destination);
        byte[] existing = File.ReadAllBytes(destination);

        Assert.Throws<IOException>(() => MapAssetCompiler.Compile(source.RootPath, 1010, destination));
        Assert.Equal(existing, File.ReadAllBytes(destination));
        Assert.Equal(1010u, MapAssetCodec.Decode(existing).MapDataId);
    }

    [Fact]
    public void CompilerRejectsUnknownMapInsteadOfSubstitutingAResource()
    {
        using TemporarySourceTree source = new();
        source.WriteStartupSnapshot();
        ContentWorkloadFixture.WriteNewbieMap(source);

        Assert.Throws<InvalidDataException>(() =>
            MapAssetCompiler.Compile(PackagedClientContentSource.Open(source.RootPath), uint.MaxValue));
    }
}
