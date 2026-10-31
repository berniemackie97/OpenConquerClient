using System.Text;
using OpenConquer.Content.Map;

namespace OpenConquer.Content.Tests.Map;

public sealed class GameMapCatalogTests
{
    [Fact]
    public void Retail5517IndexResolvesTerrainDataKeysWithoutWorldIdInference()
    {
        string retailRoot = Path.Combine(AppContext.BaseDirectory, "RetailFixture");
        GameMapCatalog catalog = GameMapCatalog.Load(new ClientContentRoot(retailRoot));

        Assert.Equal(261, catalog.Count);
        Assert.True(catalog.TryGetTerrain(1002, out GameMapCatalog.Terrain? foundCity));
        GameMapCatalog.Terrain twinCity = Assert.IsType<GameMapCatalog.Terrain>(foundCity);
        Assert.Equal("map/map/newplain.7z", twinCity.Path);
        Assert.Equal(256u, twinCity.CompanionValue);
        Assert.True(catalog.TryGetTerrain(1067, out GameMapCatalog.Terrain? foundDuplicate));
        GameMapCatalog.Terrain duplicate = Assert.IsType<GameMapCatalog.Terrain>(foundDuplicate);
        Assert.Equal("map/map/09Christmas02.7z", duplicate.Path);
        Assert.Equal(128u, duplicate.CompanionValue);
        Assert.False(catalog.TryGetTerrain(uint.MaxValue, out _));
    }

    [Fact]
    public void DuplicateKeyUsesLastPathAndCompanionValue()
    {
        GameMapCatalog catalog = GameMapCatalog.Parse(Build(
            (1067u, "map/map/qiling-z.7z", 256u),
            (1067u, "map/map/09Christmas02.7z", 128u)));

        Assert.Equal(1, catalog.Count);
        Assert.True(catalog.TryGetTerrain(1067, out GameMapCatalog.Terrain? foundTerrain));
        GameMapCatalog.Terrain terrain = Assert.IsType<GameMapCatalog.Terrain>(foundTerrain);
        Assert.Equal("map/map/09Christmas02.7z", terrain.Path);
        Assert.Equal(128u, terrain.CompanionValue);
    }

    [Fact]
    public void TruncatedOrTrailingDataIsRejected()
    {
        byte[] valid = Build((1002u, "map/map/newplain.7z", 256u));
        Assert.Throws<InvalidDataException>(() => GameMapCatalog.Parse(valid.AsSpan(0, valid.Length - 1)));
        Assert.Throws<InvalidDataException>(() => GameMapCatalog.Parse([.. valid, 0]));
        Assert.Throws<InvalidDataException>(() => GameMapCatalog.Parse([1, 0, 0, 0]));
    }

    [Fact]
    public void UnsafeOrOverlengthPathsAreRejected()
    {
        Assert.Throws<InvalidDataException>(() => GameMapCatalog.Parse(Build((1002u, "../other.7z", 256u))));
        Assert.Throws<InvalidDataException>(() => GameMapCatalog.Parse(Build((1002u, new string('x', 64), 256u))));
    }

    private static byte[] Build(params (uint Id, string Path, uint Companion)[] records)
    {
        using MemoryStream stream = new();
        using (BinaryWriter writer = new(stream, Encoding.Latin1, leaveOpen: true))
        {
            writer.Write((uint)records.Length);
            foreach ((uint id, string path, uint companion) in records)
            {
                byte[] encoded = Encoding.Latin1.GetBytes(path);
                writer.Write(id);
                writer.Write((uint)encoded.Length);
                writer.Write(encoded);
                writer.Write(companion);
            }
        }

        return stream.ToArray();
    }
}
