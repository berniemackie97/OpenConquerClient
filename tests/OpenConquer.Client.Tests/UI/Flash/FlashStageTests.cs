using System.Numerics;
using System.Text;
using OpenConquer.Client.UI.Flash;
using OpenConquer.Content.Flash;

namespace OpenConquer.Client.Tests.UI.Flash;

public sealed class FlashStageTests
{
    // Character 1: an opaque 10x10 square at the origin.
    private const string Shapes = """
        "1":{"kind":"shape","raster":0,"x":0,"y":0}
        """;

    [Fact]
    public void Tick_AdvancesTheTimelineRunsEachFrameScriptAndHonorsStop()
    {
        FlashMovie movie = Movie(Shapes, """
            "root":{"frames":[[{"d":1,"c":1,"p":1}],[{"d":1,"c":1,"p":1},{"d":2,"c":1,"p":2}],[{"d":1,"c":1,"p":1}]],"labels":{}}
            """);
        List<string> log = [];
        FlashScriptTable scripts = new();
        scripts.Frame("test", "root", 1, _ => log.Add("f1"));
        scripts.Frame("test", "root", 2, _ => log.Add("f2"));
        scripts.Frame("test", "root", 3, clip =>
        {
            log.Add("f3");
            clip.Stop();
        });
        FlashStage stage = Start(movie, scripts);

        stage.Tick();
        Assert.Equal(2, stage.Root.Children.Count);
        stage.Tick();
        stage.Tick();

        Assert.Equal(["f1", "f2", "f3"], log);
        Assert.Equal((3, 1), (stage.Root.CurrentFrame, stage.Root.Children.Count));
    }

    [Fact]
    public void Seek_KeepsAnInstanceWhileItsDepthHoldsTheSamePlacement()
    {
        FlashMovie movie = Movie(Shapes, """
            "root":{"frames":[[{"d":1,"c":1,"p":1}],[{"d":1,"c":1,"p":1,"m":[1,0,0,1,10,0]}],[{"d":1,"c":1,"p":3}]],"labels":{}}
            """);
        FlashStage stage = Start(movie, new FlashScriptTable());
        FlashDisplayObject first = stage.Root.Children[0];

        stage.Root.GotoAndStop(2);
        Assert.Same(first, stage.Root.Children[0]);
        Assert.Equal(10f, first.X);

        stage.Root.GotoAndStop(3);
        FlashDisplayObject replaced = stage.Root.Children[0];
        Assert.NotSame(first, replaced);

        stage.Root.GotoAndStop(1);
        Assert.NotSame(replaced, stage.Root.Children[0]);
        Assert.Equal(0f, stage.Root.Children[0].X);
    }

    [Fact]
    public void ScriptWrittenTransform_IsNoLongerUpdatedByTheTimeline()
    {
        FlashMovie movie = Movie(Shapes, """
            "root":{"frames":[[{"d":1,"c":1,"p":1}],[{"d":1,"c":1,"p":1,"m":[1,0,0,1,10,0],"cx":[256,256,256,64,0,0,0,0]}]],"labels":{}}
            """);
        FlashStage stage = Start(movie, new FlashScriptTable());
        FlashDisplayObject shape = stage.Root.Children[0];

        shape.Y = 7f;
        stage.Root.GotoAndStop(2);

        Assert.Equal((0f, 7f, 100f), (shape.X, shape.Y, shape.Alpha));
        Assert.True(shape.ScriptOwnsTransform);
    }

    [Fact]
    public void ParentFrameScript_RunsBeforeTheFirstFrameScriptOfAChildPlacedOnThatFrame()
    {
        FlashMovie movie = Movie(Shapes + ""","2":{"kind":"sprite"}""", """
            "root":{"frames":[[{"d":1,"c":2,"p":1,"n":"kid"}]],"labels":{}},"2":{"frames":[[{"d":1,"c":1,"p":1}]],"labels":{}}
            """);
        List<string> log = [];
        FlashScriptTable scripts = new();
        scripts.Frame("test", "root", 1, clip => log.Add(clip.ChildClip("kid").CurrentFrame == 1 ? "parent sees kid" : "parent"));
        scripts.Frame("test", "2", 1, _ => log.Add("kid"));

        Start(movie, scripts);

        Assert.Equal(["parent sees kid", "kid"], log);
    }

    [Fact]
    public void Pointer_TargetsAClipWithHandlersAndSendsRollPressAndRelease()
    {
        FlashStage stage = Start(Movie(Shapes + ""","2":{"kind":"sprite"}""", """
            "root":{"frames":[[{"d":1,"c":2,"p":1,"n":"btn","m":[1,0,0,1,100,100]}]],"labels":{}},"2":{"frames":[[{"d":1,"c":1,"p":1}]],"labels":{}}
            """), new FlashScriptTable());
        List<string> log = [];
        FlashClip button = stage.Root.ChildClip("btn");
        button.OnRollOver = _ => log.Add("over");
        button.OnRollOut = _ => log.Add("out");
        button.OnPress = _ => log.Add("press");
        button.OnRelease = _ => log.Add("release");

        stage.PointerMoved(new Vector2(99, 99));
        stage.PointerMoved(new Vector2(105, 105));
        stage.PointerPressed();
        stage.PointerReleased();
        stage.PointerMoved(new Vector2(111, 105));

        Assert.Equal(["over", "press", "release", "out"], log);
    }

    [Fact]
    public void Pointer_ReleasedAwayFromThePressedTarget_SendsOnlyReleaseOutside()
    {
        FlashStage stage = Start(Movie(Shapes + ""","2":{"kind":"sprite"}""", """
            "root":{"frames":[[{"d":1,"c":2,"p":1,"n":"btn"}]],"labels":{}},"2":{"frames":[[{"d":1,"c":1,"p":1}]],"labels":{}}
            """), new FlashScriptTable());
        List<string> log = [];
        FlashClip button = stage.Root.ChildClip("btn");
        button.OnRollOver = _ => log.Add("over");
        button.OnRollOut = _ => log.Add("out");
        button.OnReleaseOutside = _ => log.Add("outside");

        stage.PointerMoved(new Vector2(5, 5));
        stage.PointerPressed();
        stage.PointerMoved(new Vector2(50, 50));
        stage.PointerReleased();

        Assert.Equal(["over", "outside"], log);
    }

    [Fact]
    public void Pointer_UsesTheChildNamedHitAreaEvenWhenItsAlphaIsZero()
    {
        FlashStage stage = Start(Movie(Shapes + ""","2":{"kind":"sprite"},"3":{"kind":"sprite"}""", """
            "root":{"frames":[[{"d":1,"c":2,"p":1,"n":"son"}]],"labels":{}},
            "2":{"frames":[[{"d":1,"c":1,"p":1},{"d":2,"c":3,"p":1,"n":"hitArea","m":[1,0,0,1,50,0],"cx":[256,256,256,0,0,0,0,0]}]],"labels":{}},
            "3":{"frames":[[{"d":1,"c":1,"p":1}]],"labels":{}}
            """), new FlashScriptTable());
        bool over = false;
        stage.Root.ChildClip("son").OnRollOver = _ => over = true;

        stage.PointerMoved(new Vector2(5, 5));
        Assert.False(over);

        stage.PointerMoved(new Vector2(55, 5));
        Assert.True(over);
    }

    [Fact]
    public void Pointer_IgnoresTheMaskedPartOfAClip()
    {
        FlashStage stage = Start(Movie(Shapes + ""","2":{"kind":"sprite"}""", """
            "root":{"frames":[[{"d":1,"c":1,"p":1,"clip":2},{"d":2,"c":2,"p":1,"n":"btn","m":[1,0,0,1,5,0]}]],"labels":{}},
            "2":{"frames":[[{"d":1,"c":1,"p":1}]],"labels":{}}
            """), new FlashScriptTable());
        int rollOvers = 0;
        stage.Root.ChildClip("btn").OnRollOver = _ => rollOvers++;

        stage.PointerMoved(new Vector2(12, 5));
        Assert.Equal(0, rollOvers);

        stage.PointerMoved(new Vector2(7, 5));
        Assert.Equal(1, rollOvers);
    }

    [Fact]
    public void SwapDepths_ExchangesPlacesAndTheTimelineLeavesBothAlone()
    {
        FlashStage stage = Start(Movie(Shapes + ""","2":{"kind":"sprite"}""", """
            "root":{"frames":[[{"d":1,"c":2,"p":1,"n":"a"},{"d":2,"c":2,"p":1,"n":"b"}],[]],"labels":{}},
            "2":{"frames":[[{"d":1,"c":1,"p":1}]],"labels":{}}
            """), new FlashScriptTable());
        FlashClip a = stage.Root.ChildClip("a");
        FlashClip b = stage.Root.ChildClip("b");

        a.SwapDepths(2);
        Assert.Equal([b, a], stage.Root.Children);
        Assert.Equal((2, 1), (a.Depth, b.Depth));

        stage.Root.GotoAndStop(2);
        Assert.Equal([b, a], stage.Root.Children);
    }

    [Fact]
    public void LoadMovie_PlaysTheOtherMoviesMainTimelineInsideTheClip()
    {
        FlashMovie host = Movie(Shapes + ""","2":{"kind":"sprite"}""", """
            "root":{"frames":[[{"d":1,"c":2,"p":1,"n":"holder","m":[1,0,0,1,30,0]}]],"labels":{}},"2":{"frames":[[]],"labels":{}}
            """);
        FlashMovie loaded = Movie(Shapes, """
            "root":{"frames":[[{"d":4,"c":1,"p":1}],[{"d":4,"c":1,"p":1}]],"labels":{"two":2}}
            """, name: "loaded");
        List<string> log = [];
        FlashScriptTable scripts = new();
        scripts.Frame("loaded", "root", 1, clip => log.Add($"loaded at {clip.X}"));
        FlashStage stage = Start(host, scripts);

        FlashClip holder = stage.Root.ChildClip("holder");
        holder.LoadMovie(loaded);
        stage.Tick();

        Assert.Equal(["loaded at 30"], log);
        Assert.Same(loaded, holder.Movie);
        Assert.Equal((2, 4), (holder.CurrentFrame, holder.Children[0].Depth));
    }

    private static FlashStage Start(FlashMovie movie, FlashScriptTable scripts)
    {
        FlashStage stage = new(movie, scripts, new SquareRasters());
        stage.Start();
        return stage;
    }

    private static FlashMovie Movie(string characters, string timelines, string name = "test")
    {
        const string Digest = "0000000000000000000000000000000000000000000000000000000000000000";
        string json = "{\"format\":1,\"movie\":\"" + name + "\",\"source\":{\"path\":\"x\",\"sha256\":\"y\"},\"frameRate\":30," +
            "\"width\":1024,\"height\":768,\"rasters\":[" + Raster(0, Digest) + "," + Raster(1, Digest) + "]," +
            "\"characters\":{" + characters + "},\"timelines\":{" + timelines + "}}";
        return FlashMovieReader.Read(Encoding.UTF8.GetBytes(json), "Test");
    }

    private static string Raster(int index, string digest) =>
        "{\"path\":\"r" + index + ".rgba.br\",\"width\":10,\"height\":10,\"sha256\":\"" + digest + "\"}";

    private sealed class SquareRasters : IFlashRasterSource
    {
        public ReadOnlyMemory<byte> Pixels(FlashMovie movie, int index)
        {
            byte[] pixels = new byte[10 * 10 * 4];
            Array.Fill(pixels, byte.MaxValue);
            return pixels;
        }
    }
}
