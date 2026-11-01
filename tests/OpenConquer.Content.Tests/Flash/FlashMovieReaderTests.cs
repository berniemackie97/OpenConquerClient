using System.Text;
using OpenConquer.Content.Flash;

namespace OpenConquer.Content.Tests.Flash;

public sealed class FlashMovieReaderTests
{
    private const string Raster = """{"path":"r0.rgba.br","width":2,"height":2,"sha256":"0000000000000000000000000000000000000000000000000000000000000000"}""";

    [Fact]
    public void Read_BuildsSpritesShapesAndPlacements()
    {
        FlashMovie movie = Read(characters: """{"1":{"kind":"shape","pieces":[{"raster":0,"x":-1.5,"y":2,"snap":"center"}]},"2":{"kind":"sprite"}}""",
            timelines: """{"root":{"frames":[[{"d":1,"c":2,"p":1,"n":"clip","m":[2,0,0,2,10,20],"cx":[256,256,256,128,0,0,0,0]}]],"labels":{"go":1}},"2":{"frames":[[{"d":3,"c":1,"p":1}]],"labels":{}}}""");

        FlashPlacement clip = Assert.Single(movie.Root.DisplayList(1));
        Assert.Equal(("clip", new FlashMatrix(2f, 0f, 0f, 2f, 10f, 20f), 128), (clip.Name, clip.Matrix, clip.ColorTransform.AlphaMultiplier));
        FlashSpriteCharacter sprite = Assert.IsType<FlashSpriteCharacter>(movie.Character(2));
        FlashRasterCharacter shape = Assert.IsType<FlashRasterCharacter>(movie.Character(sprite.Timeline.DisplayList(1)[0].CharacterId));
        Assert.False(shape.IsStaticText);
        Assert.Equal(new FlashRasterPiece(0, -1.5f, 2f, FlashPixelSnap.PixelCenter), Assert.Single(shape.Pieces));
        Assert.Equal(1, movie.Root.Labels["go"]);
    }

    [Theory]
    [InlineData("""{"1":{"kind":"video"}}""", """{"root":{"frames":[[]],"labels":{}}}""")]
    [InlineData("""{"1":{"kind":"shape","pieces":[{"raster":0,"x":0,"y":0,"snap":"center"}]}}""", """{"root":{"frames":[[{"d":1,"c":9,"p":1}]],"labels":{}}}""")]
    [InlineData("""{"1":{"kind":"shape","pieces":[{"raster":1,"x":0,"y":0,"snap":"center"}]}}""", """{"root":{"frames":[[]],"labels":{}}}""")]
    [InlineData("""{"1":{"kind":"shape","pieces":[{"raster":0,"x":0,"y":0,"snap":"center"}]}}""", """{"root":{"frames":[[{"d":2,"c":1,"p":1},{"d":1,"c":1,"p":1}]],"labels":{}}}""")]
    [InlineData("""{"1":{"kind":"shape","pieces":[{"raster":0,"x":0,"y":0,"snap":"center"}]}}""", """{"root":{"frames":[[{"d":1,"c":1,"p":2}]],"labels":{}}}""")]
    [InlineData("""{"1":{"kind":"shape","pieces":[{"raster":0,"x":0,"y":0,"snap":"center"}]}}""", """{"root":{"frames":[[{"d":1,"c":1,"p":1,"r":5}]],"labels":{}}}""")]
    [InlineData("""{"1":{"kind":"morph","x":0,"y":0,"rasters":{"0":0}}}""", """{"root":{"frames":[[{"d":1,"c":1,"p":1}]],"labels":{}}}""")]
    [InlineData("""{"1":{"kind":"shape","pieces":[{"raster":0,"x":0,"y":0,"snap":"center"}]}}""", """{"root":{"frames":[[]],"labels":{"x":2}}}""")]
    [InlineData("""{"1":{"kind":"shape","pieces":[{"raster":0,"x":0,"y":0,"snap":"center"}]}}""", """{"root":{"frames":[[]],"labels":{}},"7":{"frames":[[]],"labels":{}}}""")]
    public void Read_RejectsInconsistentMovies(string characters, string timelines)
    {
        Assert.Throws<InvalidDataException>(() => Read(characters, timelines));
    }

    [Fact]
    public void Read_RejectsRasterPathsOutsideTheMovie()
    {
        string json = Movie("""{"path":"../r0.rgba.br","width":2,"height":2,"sha256":"0000000000000000000000000000000000000000000000000000000000000000"}""",
            "{}", """{"root":{"frames":[[]],"labels":{}}}""");

        Assert.Throws<InvalidDataException>(() => FlashMovieReader.Read(Encoding.UTF8.GetBytes(json), "Role/test"));
    }

    [Fact]
    public void Read_VideoHoldsItsLastFrameForRatiosWithoutOne()
    {
        FlashMovie movie = Read(characters: """{"1":{"kind":"video","width":2,"height":2,"frames":{"0":0,"4":0}}}""",
            timelines: """{"root":{"frames":[[{"d":1,"c":1,"p":1,"r":0}],[{"d":1,"c":1,"p":1,"r":5}]],"labels":{}}}""");

        FlashVideoCharacter video = Assert.IsType<FlashVideoCharacter>(movie.Character(1));

        Assert.Equal((0, 0, 0), (video.RasterAt(0), video.RasterAt(3), video.RasterAt(5)));
    }

    [Theory]
    [InlineData("""{"1":{"kind":"video","width":2,"height":2,"frames":{"4":0}}}""")]
    [InlineData("""{"1":{"kind":"shape","pieces":[]}}""")]
    [InlineData("""{"1":{"kind":"shape","pieces":[{"raster":0,"x":0,"y":0,"snap":"edge"}]}}""")]
    public void Read_RejectsAVideoWithoutFrameZeroAndMalformedPieces(string characters)
    {
        Assert.Throws<InvalidDataException>(() => Read(characters, """{"root":{"frames":[[]],"labels":{}}}"""));
    }

    private static FlashMovie Read(string characters, string timelines) =>
        FlashMovieReader.Read(Encoding.UTF8.GetBytes(Movie(Raster, characters, timelines)), "Role/test");

    private static string Movie(string raster, string characters, string timelines) =>
        $$"""{"format":1,"movie":"test","source":{"path":"x","sha256":"y"},"frameRate":30,"width":1024,"height":768,"rasters":[{{raster}}],"characters":{{characters}},"timelines":{{timelines}}}""";
}
