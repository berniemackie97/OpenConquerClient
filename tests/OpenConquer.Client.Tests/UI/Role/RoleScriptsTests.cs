using System.Numerics;
using System.Text;
using OpenConquer.Client.UI.Flash;
using OpenConquer.Client.UI.Role;
using OpenConquer.Content.Flash;
using OpenConquer.Content.Role;

namespace OpenConquer.Client.Tests.UI.Role;

public sealed class RoleScriptsTests
{
    private const string Ini = "[logo]\r\nxPos=776\r\nyPos=2\r\n[info]\r\ninfo5=Monks\\nand saints\r\ninfoII5=Through training\r\n" +
                               "[affirmPanel]\r\nshape0=Big\r\nshape1=Small\r\nvocation10=Monk\r\n";

    [Fact]
    public void Next_WithoutAClass_ShowsTheProfessionErrorUntilOk()
    {
        Flow flow = Flow.ToClassStage();

        flow.Click(flow.Root.ChildClip("nextBtn"));
        FlashClip error = flow.Root.ChildClip("errorMC");
        flow.TickUntil(() => error.CurrentFrame == 6);
        Assert.Equal("Please select your profession!", error.ChildText("txt").Text);
        Assert.False(flow.Root.ChildClip("returnBtn").Enabled);

        flow.Click((FlashButtonInstance)error.Child("okBtn")!);
        flow.TickUntil(() => error.CurrentFrame == 1);
        Assert.True(flow.Root.ChildClip("returnBtn").Enabled);
        Assert.Equal(3, flow.Root.CurrentFrame);
    }

    [Fact]
    public void CreatingAMonk_SendsTheNativeCommandsAndStartsLoading()
    {
        Flow flow = Flow.ToBodyStage();
        FlashClip body = flow.Root.ChildClip("sb");

        flow.Click(body.ChildClip("bodySizeBtn_1"));
        flow.Type("Probe5517");
        flow.Click(flow.Root.ChildClip("createBtn"));
        FlashClip panel = flow.Root.ChildClip("createCharacterMC");
        flow.TickUntil(() => panel.CurrentFrame == 6);
        Assert.Equal(("Probe5517", "Monk", "Small"), (panel.ChildText("userNameTxt").Text, panel.ChildText("jobTxt").Text, panel.ChildText("bodyTxt").Text));

        flow.Click((FlashButtonInstance)panel.Child("okBtn")!);

        Assert.Equal(["CMD_NICK Probe5517", "CMD_BODYSIZE 1", "CMD_VOCATION 10", "CMD_ENTERGAME 1"], flow.Commands);
        flow.Tick();
        Assert.True(flow.Root.ChildClip("inGameMC").Get<bool>("isLoading"));
    }

    [Fact]
    public void ClickingBodySilhouettesSelectsTheirRespectiveSizes()
    {
        Flow flow = Flow.ToBodyStage();
        FlashClip body = flow.Root.ChildClip("sb");

        flow.ClickAt(285, 430);
        FlashClip[] inspected = [body, body.ChildClip("sb"), body.ChildClip("sm")];
        string description = string.Join("; ", inspected
            .Select(clip => $"{clip.Name}/{clip.TimelineKey}/f{clip.CurrentFrame}/m{FlashTransforms.World(clip)}: " +
                string.Join(", ", clip.Children.Select(child =>
                {
                    string shape = child is FlashShapeInstance raster ?
                        $"{raster.Character.Id}:{raster.Movie.Rasters[FlashTransforms.RasterOf(raster.Character, raster.Ratio).Raster].Width}x" +
                        $"{raster.Movie.Rasters[FlashTransforms.RasterOf(raster.Character, raster.Ratio).Raster].Height}" :
                        child.Character.GetType().Name;
                    return $"{child.Name}/{shape}/m{FlashTransforms.World(child)}";
                }))));
        Assert.True(body.Get<int>("nSelectBody") == 1, description);
        Assert.Equal(3, body.ChildClip("sb").CurrentFrame);
        Assert.Equal(3, body.ChildClip("bodySizeBtn_0").CurrentFrame);

        flow.ClickAt(567, 510);
        Assert.Equal(2, body.Get<int>("nSelectBody"));
        Assert.Equal(3, body.ChildClip("sm").CurrentFrame);
        Assert.Equal(3, body.ChildClip("bodySizeBtn_1").CurrentFrame);
    }

    [Theory]
    [InlineData(false, true, "Please enter your First Name!")]
    [InlineData(true, false, "Please select your body size.")]
    public void Create_WithoutABodyOrAName_ShowsItsError(bool typeName, bool chooseBody, string expected)
    {
        Flow flow = Flow.ToBodyStage();
        if (chooseBody)
        {
            flow.Click(flow.Root.ChildClip("sb").ChildClip("bodySizeBtn_0"));
        }

        if (typeName)
        {
            flow.Type("Probe");
        }

        flow.Click(flow.Root.ChildClip("createBtn"));
        FlashClip error = flow.Root.ChildClip("errorMC");
        flow.TickUntil(() => error.CurrentFrame == 6);

        Assert.Equal(expected, error.ChildText("txt").Text);
        Assert.Empty(flow.Commands);
    }

    [Fact]
    public void Name_StopsAtFifteenBytes()
    {
        Flow flow = Flow.ToBodyStage();

        flow.Type("abcdefghijklmnopq");

        Assert.Equal("abcdefghijklmno", flow.Root.ChildClip("sb").ChildText("userNameTxt").Text);
    }

    [Fact]
    public void Name_PasteAndMiddleInsertionRejectOverflowWithoutChangingExistingName()
    {
        Flow flow = Flow.ToBodyStage();
        FlashTextInstance name = flow.Root.ChildClip("sb").ChildText("userNameTxt");

        flow.Type("abcdefghijklmn");
        name.MoveCaret(7);
        flow.EnterText("XY");
        Assert.Equal("abcdefghijklmn", name.Text);
        Assert.Equal(7, name.Caret);

        name.SetSelection(5, 9);
        flow.EnterText("PQ");
        Assert.Equal("abcdePQjklmn", name.Text);
        Assert.Equal(7, name.Caret);

        name.MoveCaret(0);
        flow.EnterText("éééééééé");
        Assert.Equal("abcdePQjklmn", name.Text);
        Assert.Equal(0, name.Caret);
    }

    [Fact]
    public void HostError_ShowsItsTextAndStopsTheLoadingClip()
    {
        Flow flow = Flow.ToClassStage();
        flow.Root.ChildClip("inGameMC").GotoAndPlay(2);

        flow.Scripts.Receive("ERROR|First Name should be 4-15 characters!");
        FlashClip error = flow.Root.ChildClip("errorMC");
        flow.TickUntil(() => error.CurrentFrame == 6);

        Assert.Equal("First Name should be 4-15 characters!", error.ChildText("txt").Text);
        Assert.Equal(1, flow.Root.ChildClip("inGameMC").CurrentFrame);
    }

    [Fact]
    public void Back_OnTheClassStage_AsksTheHostToReturnToLogin()
    {
        Flow flow = Flow.ToClassStage();

        flow.Click(flow.Root.ChildClip("returnBtn"));

        Assert.Equal(["CMD_DRIVERBACK "], flow.Commands);
    }

    [Fact]
    public void NameEqualToTheAccount_AsksForConfirmationBeforeSubmitting()
    {
        Flow flow = Flow.ToBodyStage();
        flow.Scripts.Receive("USERACCOUNT|probe");
        flow.Click(flow.Root.ChildClip("sb").ChildClip("bodySizeBtn_0"));
        flow.Type("probe");
        flow.Click(flow.Root.ChildClip("createBtn"));
        FlashClip panel = flow.Root.ChildClip("createCharacterMC");
        flow.TickUntil(() => panel.CurrentFrame == 6);

        flow.Click((FlashButtonInstance)panel.Child("okBtn")!);
        FlashClip warning = panel.ChildClip("mcSafe");
        Assert.True(warning.Visible);
        Assert.Empty(flow.Commands);

        flow.Click((FlashButtonInstance)warning.Child("okBtn")!);
        Assert.False(warning.Visible);
        Assert.Equal("CMD_NICK probe", flow.Commands[0]);
    }

    private sealed class Flow : IRoleCommands, IRoleSounds
    {
        private readonly FlashStage _stage;

        private Flow()
        {
            FlashScriptTable scripts = new();
            Scripts = new RoleScripts(SelectCharacterIni.Parse(Encoding.ASCII.GetBytes(Ini)), RoleMovies.LoadCharacterInAndOut(), this, this);
            Scripts.Register(scripts);
            new CharacterInAndOutScripts(Scripts).Register(scripts);
            _stage = new FlashStage(RoleMovies.LoadRole(), scripts, new FlashRasterCache());
            _stage.Start();
            Scripts.Receive("PATH|");
        }

        public RoleScripts Scripts
        {
            get;
        }

        public List<string> Commands { get; } = [];

        public FlashClip Root => _stage.Root;

        public static Flow ToClassStage()
        {
            Flow flow = new();
            flow.TickUntil(() => flow.Root.CurrentFrame == 3 && flow.Root.ChildClip("characterInAndOut").CurrentFrame == 132);
            return flow;
        }

        public static Flow ToBodyStage()
        {
            Flow flow = ToClassStage();
            FlashClip monk = flow.Root.ChildClip("characterInAndOut").ChildClip("mcRole").ChildClip("son_6");
            flow.Click(monk.ChildClip("hitArea"));
            flow.Click(flow.Root.ChildClip("nextBtn"));
            flow.TickUntil(() => flow.Root.CurrentFrame == 5 && flow.Root.Child("sb") is FlashClip { CurrentFrame: > 36 });
            return flow;
        }

        public void Command(string command, string argument) => Commands.Add($"{command} {argument}");

        public void Play(string set, string handle)
        {
        }

        public void Tick() => _stage.Tick();

        public void TickUntil(Func<bool> condition)
        {
            for (int tick = 0; tick < 600 && !condition(); tick++)
            {
                _stage.Tick();
            }

            Assert.True(condition(), "The stage did not reach the expected state within 600 frames.");
        }

        public void Type(string text)
        {
            foreach (char character in text)
            {
                _stage.TextEntered(character);
            }
        }

        public void EnterText(string text) => _stage.TextEntered(text);

        public void ClickAt(float x, float y)
        {
            _stage.PointerMoved(new Vector2(x, y));
            _stage.PointerPressed();
            _stage.PointerReleased();
        }

        /// <summary>Moves to the middle of the first shape of <paramref name="shapeOwner"/>, presses and releases.</summary>
        public void Click(FlashDisplayObject shapeOwner)
        {
            _stage.PointerMoved(Center(shapeOwner));
            _stage.PointerPressed();
            _stage.PointerReleased();
        }

        private static Vector2 Center(FlashDisplayObject owner)
        {
            switch (owner)
            {
                case FlashShapeInstance shape:
                    (int raster, float x, float y) = FlashTransforms.RasterOf(shape.Character, shape.Ratio);
                    FlashRaster info = shape.Movie.Rasters[raster];
                    return Vector2.Transform(new Vector2(x + info.Width / 2f, y + info.Height / 2f), FlashTransforms.World(shape));
                case FlashButtonInstance button:
                    FlashButtonRecord hit = button.Definition.Records.First(record => (record.States & FlashButtonStates.Hit) != 0);
                    (int hitRaster, float hx, float hy) = FlashTransforms.RasterOf(button.Movie.Character(hit.CharacterId), null);
                    FlashRaster hitInfo = button.Movie.Rasters[hitRaster];
                    return Vector2.Transform(new Vector2(hx + hitInfo.Width / 2f, hy + hitInfo.Height / 2f),
                        FlashTransforms.ToMatrix(hit.Matrix) * FlashTransforms.World(button));
                case FlashClip clip:
                    return Center(clip.Children.First(child => child is FlashShapeInstance or FlashClip { Children.Count: > 0 } && child.ClipDepth is null));
                default:
                    throw new InvalidOperationException($"Cannot find the middle of {owner.GetType().Name}.");
            }
        }
    }
}
