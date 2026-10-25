using System.Globalization;
using System.Numerics;
using OpenConquer.Client.UI.Flash;

namespace OpenConquer.Client.UI.Role;

/// <summary>The role.dat root functions characterInAndOut calls through <c>_parent</c>.</summary>
internal interface ICharacterSelectionHost
{
    /// <summary><c>onRollOverItem(id)</c>.</summary>
    void RollOverItem(int id);

    /// <summary><c>onPressItem(id)</c>.</summary>
    void PressItem(int id);

    /// <summary><c>onRollOutItem(id)</c>.</summary>
    void RollOutItem(int id);
}

/// <summary>
/// The ActionScript of <c>characterInAndOut.swf</c>, translated statement for statement (decoded scripts in
/// <c>create-role-research/decode/characterInAndOut</c>; contract in 20_notes Architecture/12).
/// </summary>
internal sealed class CharacterInAndOutScripts
{
    public const string Movie = "characterInAndOut";

    private const float StageHeight = 768f;
    private const int HighestDepth = 100;

    // Sons by sprite: id (frame 1 script) and z (mcRole init()).
    private static readonly Dictionary<int, int> s_sonIds = new()
    {
        [91] = 6,
        [105] = 5,
        [116] = 2,
        [127] = 1,
        [138] = 8,
        [149] = 7,
        [160] = 11,
        [171] = 9,
        [182] = 12,
        [193] = 10,
        [204] = 3,
        [215] = 4,
    };

    private static readonly float[] s_sonZ = [-350f, -300f, -260f, -200f, -100f, -50f, 0f, 0f, -200f, -450f, -500f, -1200f];

    private static readonly int[] s_hitAreas = [81, 99, 110, 121, 132, 143, 154, 165, 176, 187, 198, 209];

    private readonly ICharacterSelectionHost _host;

    public CharacterInAndOutScripts(ICharacterSelectionHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
    }

    public void Register(FlashScriptTable scripts)
    {
        ArgumentNullException.ThrowIfNull(scripts);

        scripts.Frame(Movie, "root", 1, root => root.Set("isLoaded", true));
        scripts.Frame(Movie, "root", 132, RootFrame132);
        scripts.Frame(Movie, "root", 133, root =>
        {
            DisableRole(root);
            root.OnEnterFrame = null;
        });
        scripts.Frame(Movie, "root", 144, root =>
        {
            root.Parent?.Play();
            root.Stop();
        });

        foreach ((int sprite, int id) in s_sonIds)
        {
            string timeline = sprite.ToString(CultureInfo.InvariantCulture);
            scripts.Frame(Movie, timeline, 1, son =>
            {
                son.Stop();
                son.Set("id", id);
            });
            scripts.Frame(Movie, timeline, 10, son => son.Stop());
            scripts.Frame(Movie, timeline, 20, son => son.Stop());
        }

        foreach (int hitArea in s_hitAreas)
        {
            scripts.Frame(Movie, hitArea.ToString(CultureInfo.InvariantCulture), 1, clip => clip.Alpha = 0f);
        }

        scripts.Frame(Movie, "13", 1, clip => clip.Visible = false);
        scripts.Frame(Movie, "18", 1, clip => clip.Visible = false);
        scripts.Frame(Movie, "79", 1, clip => clip.GotoAndStop(1));
        scripts.Frame(Movie, "79", 31, clip => clip.GotoAndPlay(2));
        scripts.Frame(Movie, "220", 1, CarouselFrame1);
        scripts.Frame(Movie, "223", 1, clip => clip.Stop());
    }

    /// <summary><c>inPlay()</c>: role advances to the class stage and the portraits play in from frame 1.</summary>
    public static void InPlay(FlashClip characterInAndOut)
    {
        ArgumentNullException.ThrowIfNull(characterInAndOut);

        characterInAndOut.Parent?.NextFrame();
        characterInAndOut.GotoAndPlay(1);
    }

    /// <summary><c>outPlay()</c>: the portraits play out from <c>OUT</c>.</summary>
    public static void OutPlay(FlashClip characterInAndOut)
    {
        ArgumentNullException.ThrowIfNull(characterInAndOut);

        characterInAndOut.GotoAndStop("OUT");
        characterInAndOut.Play();
    }

    public static bool IsLoaded(FlashClip characterInAndOut) => characterInAndOut.Get<bool>("isLoaded");

    private void RootFrame132(FlashClip root)
    {
        root.Stop();
        RootState state = new(root);
        FlashClip mcRole = root.ChildClip("mcRole");
        for (int index = 0; index < 12; index++)
        {
            InitRoleSon(state, mcRole.ChildClip($"son_{index}"));
        }

        root.OnMouseMove = clip =>
        {
            state.MouseMoved = true;
            clip.OnMouseMove = null;
        };
        root.OnEnterFrame = _ =>
        {
            if (state.MouseMoved)
            {
                state.SetAlpha?.Invoke();
            }
        };
    }

    private void InitRoleSon(RootState state, FlashClip son)
    {
        son.Set("orgDepth", son.Depth);
        son.Set("selected", false);
        son.OnRollOver = clip =>
        {
            SetOthersLowAlpha(state, clip);
            clip.SwapDepths(HighestDepth);
            if (!clip.Get<bool>("selected"))
            {
                clip.GotoAndPlay(2);
                _host.RollOverItem(clip.Get<int>("id"));
            }
        };
        son.OnRollOut = son.OnReleaseOutside = clip =>
        {
            SetAllFullAlpha(state);
            clip.SwapDepths(clip.Get<int>("orgDepth"));
            if (!clip.Get<bool>("selected"))
            {
                clip.GotoAndPlay(20 - clip.CurrentFrame);
                _host.RollOutItem(clip.Get<int>("id"));
            }
        };
        son.OnPress = clip =>
        {
            if (!clip.Get<bool>("selected"))
            {
                SelectRoleSon(state, clip);
            }
        };
    }

    private void SelectRoleSon(RootState state, FlashClip son)
    {
        if (state.Selected is { } previous)
        {
            previous.Set("selected", false);
            previous.GotoAndPlay(11);
            SetEffects(previous, shown: false);
        }

        state.Selected = son;
        son.Set("selected", true);
        son.GotoAndStop(11);
        SetEffects(son, shown: true);
        son.Alpha = 100f;
        _host.PressItem(son.Get<int>("id"));
    }

    private static void SetEffects(FlashClip son, bool shown)
    {
        // selectedEffect.show() plays the foot glow from frame 2; hide() stops on its empty frame 1.
        if (son.Child("selectedEffect") is FlashClip glow)
        {
            if (shown)
            {
                glow.GotoAndPlay(2);
            }
            else
            {
                glow.GotoAndStop(1);
            }
        }

        if (son.Child("selectedEffect_1") is FlashClip outline)
        {
            outline.Visible = shown;
        }
    }

    private static void SetOthersLowAlpha(RootState state, FlashClip focused)
    {
        focused.Alpha = 100f;
        state.TargetAlpha = 70f;
        state.SetAlpha = () =>
        {
            state.Alpha -= (state.Alpha - state.TargetAlpha) / 10f;
            foreach (FlashClip son in Sons(state.Root))
            {
                if (!ReferenceEquals(son, focused))
                {
                    son.Alpha = state.Alpha;
                }
            }

            SnapAlpha(state);
        };
    }

    private static void SetAllFullAlpha(RootState state)
    {
        state.TargetAlpha = 100f;
        state.SetAlpha = () =>
        {
            state.Alpha -= (state.Alpha - state.TargetAlpha) / 10f;
            foreach (FlashClip son in Sons(state.Root))
            {
                son.Alpha = state.Alpha;
            }

            SnapAlpha(state);
        };
    }

    private static void SnapAlpha(RootState state)
    {
        if (MathF.Abs(state.Alpha - state.TargetAlpha) < 3f)
        {
            state.Alpha = state.TargetAlpha;
            state.SetAlpha = null;
        }
    }

    private static void DisableRole(FlashClip root)
    {
        FlashClip mcRole = root.ChildClip("mcRole");
        mcRole.Set("bActived", false);
        foreach (FlashClip son in Sons(root))
        {
            son.OnRollOver = null;
            son.OnRollOut = null;
            son.OnPress = null;
        }
    }

    private static IEnumerable<FlashClip> Sons(FlashClip root)
    {
        FlashClip mcRole = root.ChildClip("mcRole");
        for (int index = 0; index < 12; index++)
        {
            yield return mcRole.ChildClip($"son_{index}");
        }
    }

    private static void CarouselFrame1(FlashClip mcRole)
    {
        CarouselState state = new(mcRole.ChildClip("bgPic").X);
        mcRole.Set("bActived", true);

        // init()
        FlashClip[] sons = new FlashClip[12];
        for (int index = 0; index < 12; index++)
        {
            sons[index] = mcRole.ChildClip($"son_{index}");
            state.Z[index] = s_sonZ[index];
            state.X[index] = sons[index].X;
        }

        SetPosition(mcRole, sons, state, state.CamX);

        mcRole.OnMouseMove = clip =>
        {
            state.MouseMoved = true;
            clip.OnMouseMove = null;
        };
        mcRole.OnEnterFrame = clip =>
        {
            if (!clip.Get<bool>("bActived") || !state.MouseMoved)
            {
                return;
            }

            const float MaxSpeed = 50f;
            Vector2 mouse = clip.Stage.LocalPointer(clip);
            if (mouse.Y > 100f && mouse.Y < StageHeight - 100f && (mouse.X < -400f || mouse.X > 400f))
            {
                state.CamTargetX = mouse.X * CarouselState.WidthLimitArea;
                if (state.Acceleration < CarouselState.MaxAcceleration)
                {
                    state.Acceleration += 0.002f;
                    if (state.Acceleration >= CarouselState.MaxAcceleration)
                    {
                        state.Acceleration = CarouselState.MaxAcceleration;
                    }
                }
            }
            else if (state.Acceleration > 0f)
            {
                state.Acceleration -= 0.01f;
                if (state.Acceleration <= 0f)
                {
                    state.Acceleration = 0f;
                }
            }

            float distance = state.CamTargetX - state.CamX;
            float speed = Math.Clamp(distance * state.Acceleration, -MaxSpeed, MaxSpeed);
            if (MathF.Abs(distance) < 3f)
            {
                speed = 0f;
                state.Acceleration = 0f;
            }

            state.CamX = Round(state.CamX + speed);
            SetPosition(clip, sons, state, state.CamX);
        };
        mcRole.Stop();
    }

    private static void SetPosition(FlashClip mcRole, FlashClip[] sons, CarouselState state, float camX)
    {
        for (int index = 0; index < sons.Length; index++)
        {
            sons[index].X = Round(-CarouselState.CamZ * (camX - state.X[index]) / (CarouselState.CamZ - state.Z[index]));
        }

        mcRole.ChildClip("bgPic").X = state.BackgroundOriginX - camX * 0.3f;
    }

    /// <summary>AS2 <c>Math.round</c>: halves round up.</summary>
    private static float Round(float value) => MathF.Floor(value + 0.5f);

    private sealed class RootState(FlashClip root)
    {
        public FlashClip Root { get; } = root;

        public FlashClip? Selected
        {
            get; set;
        }

        public float Alpha { get; set; } = 100f;

        public float TargetAlpha
        {
            get; set;
        }

        public Action? SetAlpha
        {
            get; set;
        }

        public bool MouseMoved
        {
            get; set;
        }
    }

    private sealed class CarouselState(float backgroundOriginX)
    {
        public const float CamZ = 3000f;
        public const float MaxAcceleration = 0.2f;
        public const float WidthLimitArea = 2.4f;

        public float[] Z { get; } = new float[12];

        public float[] X { get; } = new float[12];

        public float BackgroundOriginX { get; } = backgroundOriginX;

        public float CamX { get; set; } = 200f;

        public float CamTargetX { get; set; } = 200f;

        public float Acceleration
        {
            get; set;
        }

        public bool MouseMoved
        {
            get; set;
        }
    }
}
