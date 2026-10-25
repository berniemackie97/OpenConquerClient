using System.Numerics;
using OpenConquer.Client.UI.Flash;
using OpenConquer.Client.UI.Role;
using OpenConquer.Content.Flash;

namespace OpenConquer.Client.Tests.UI.Role;

public sealed class CharacterInAndOutScriptsTests
{
    [Fact]
    public void Playback_StopsOnTheClassStripWithThePortraitsProjectedFromCamX200()
    {
        (FlashStage stage, _) = Play(131);

        Assert.Equal(132, stage.Root.CurrentFrame);
        Assert.False(stage.Root.IsPlaying);
        FlashClip mcRole = stage.Root.ChildClip("mcRole");

        // _x = round(-3000 * (200 - x) / (3000 - z)), son_0 at x -1675.55, z -350; bgPic at -970.5 - 0.3 * 200.
        Assert.Equal(-1680f, mcRole.ChildClip("son_0").X);
        Assert.Equal(-1030.5f, mcRole.ChildClip("bgPic").X);
        Assert.All(Enumerable.Range(0, 12), index => Assert.Equal(1, mcRole.ChildClip($"son_{index}").CurrentFrame));
        Assert.Equal(0f, mcRole.ChildClip("son_0").ChildClip("hitArea").Alpha);
        Assert.False(mcRole.ChildClip("son_0").ChildClip("selectedEffect_1").Visible);
    }

    [Fact]
    public void Portraits_RollOverPressAndRollOutAsTheRetailScriptDoes()
    {
        (FlashStage stage, Host host) = Play(131);
        FlashClip mcRole = stage.Root.ChildClip("mcRole");
        FlashClip monk = mcRole.ChildClip("son_6");
        Vector2 onMonk = PointInside(stage, monk);

        stage.PointerMoved(onMonk);
        Assert.Equal(["over 11"], host.Log);
        Assert.Equal(100, monk.Depth);
        for (int frame = 0; frame < 60; frame++)
        {
            stage.Tick();
        }

        Assert.Equal(10, monk.CurrentFrame);
        // The script writes _alpha before it snaps its own counter to 70, so the others stop just above 70%.
        float settled = mcRole.ChildClip("son_0").Alpha;
        Assert.InRange(settled, 70f, 73f);
        stage.Tick();
        Assert.Equal(settled, mcRole.ChildClip("son_0").Alpha);
        Assert.Equal(100f, monk.Alpha);

        stage.PointerPressed();
        Assert.Equal(["over 11", "press 11"], host.Log);
        Assert.Equal(11, monk.CurrentFrame);
        Assert.Equal(2, monk.ChildClip("selectedEffect").CurrentFrame);
        Assert.True(monk.ChildClip("selectedEffect_1").Visible);

        stage.PointerReleased();
        stage.PointerMoved(new Vector2(512f, 20f));
        Assert.Equal(["over 11", "press 11"], host.Log);
        Assert.Equal(monk.Get<int>("orgDepth"), monk.Depth);
    }

    private static (FlashStage Stage, Host Host) Play(int ticks)
    {
        Host host = new();
        FlashScriptTable scripts = new();
        new CharacterInAndOutScripts(host).Register(scripts);
        FlashStage stage = new(RoleMovies.LoadCharacterInAndOut(), scripts, new FlashRasterCache());
        stage.Start();
        for (int tick = 0; tick < ticks; tick++)
        {
            stage.Tick();
        }

        return (stage, host);
    }

    private static Vector2 PointInside(FlashStage stage, FlashClip son)
    {
        // The middle of the son's hit area, in stage pixels.
        FlashClip hitArea = son.ChildClip("hitArea");
        FlashShapeInstance shape = Assert.IsType<FlashShapeInstance>(Assert.Single(hitArea.Children));
        FlashRasterCharacter character = Assert.IsType<FlashRasterCharacter>(shape.Character);
        FlashRaster raster = stage.Root.Movie.Rasters[character.Raster];
        Vector2 local = new(character.OriginX + raster.Width / 2f, character.OriginY + raster.Height / 2f);
        return Vector2.Transform(local, FlashTransforms.World(shape));
    }

    private sealed class Host : ICharacterSelectionHost
    {
        public List<string> Log { get; } = [];

        public void RollOverItem(int id) => Log.Add($"over {id}");

        public void PressItem(int id) => Log.Add($"press {id}");

        public void RollOutItem(int id) => Log.Add($"out {id}");
    }
}
