using System.Globalization;
using OpenConquer.Client.UI.Flash;
using OpenConquer.Content.Flash;
using OpenConquer.Content.Role;

namespace OpenConquer.Client.UI.Role;

/// <summary>role.dat's <c>fscommand</c> calls to the host (CMD_NICK, CMD_ENTERGAME, CMD_DRIVERBACK, …).</summary>
internal interface IRoleCommands
{
    void Command(string command, string argument);
}

/// <summary>role.dat's <c>playSound(set, handle)</c>: a sound named by ini sections classSelection,
/// bodySelection or dialogBox (<c>snd_cs</c>, <c>snd_bs</c>, <c>snd_db</c>).</summary>
internal interface IRoleSounds
{
    void Play(string set, string handle);
}

/// <summary>
/// The ActionScript of role.dat, translated statement for statement (decoded scripts in
/// <c>create-role-research/decode/role</c>; contract in 20_notes Architecture/12 "role.dat runtime contract").
/// This part covers the stage frames, host messages, the class stage, the error panel and the loading clip.
/// </summary>
internal sealed partial class RoleScripts : ICharacterSelectionHost
{
    public const string Movie = "role";

    private const int EnterKey = 13;
    private const int EscapeKey = 27;

    private readonly SelectCharacterIni _ini;
    private readonly FlashMovie _characterInAndOut;
    private readonly IRoleCommands _commands;
    private readonly IRoleSounds _sounds;

    private FlashClip? _root;

    public RoleScripts(SelectCharacterIni ini, FlashMovie characterInAndOut, IRoleCommands commands, IRoleSounds sounds)
    {
        ArgumentNullException.ThrowIfNull(ini);
        ArgumentNullException.ThrowIfNull(characterInAndOut);
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(sounds);

        _ini = ini;
        _characterInAndOut = characterInAndOut;
        _commands = commands;
        _sounds = sounds;
    }

    /// <summary>Root variables of role.dat (<c>_root.nSelectJob</c>, …).</summary>
    public int SelectJob
    {
        get; private set;
    }

    public int SelectBody
    {
        get; private set;
    }

    public string UserName { get; private set; } = string.Empty;

    public string UserAccount { get; private set; } = string.Empty;

    public int Error
    {
        get; private set;
    }

    public string ErrorText { get; private set; } = string.Empty;

    public bool IsErrorPanelShown
    {
        get; private set;
    }

    private Action _pressEnterNormal = static () => { };
    private Action _pressEnterError = static () => { };
    private Action _pressEscNormal = static () => { };
    private Action _pressEscError = static () => { };

    private FlashClip Root => _root ?? throw new InvalidOperationException("role.dat has not started.");

    public void Register(FlashScriptTable scripts)
    {
        ArgumentNullException.ThrowIfNull(scripts);

        scripts.Frame(Movie, "root", 1, RootFrame1);
        scripts.Frame(Movie, "root", 2, RootFrame2);
        scripts.Frame(Movie, "root", 3, RootFrame3);
        scripts.Frame(Movie, "root", 4, RootFrame4);
        RegisterBodyStage(scripts);

        // Buttons and lists that only stop on every frame (tabEnabled has no effect on the pointer).
        Stops(scripts, "48", 12);
        Stops(scripts, "122", 4);
        Stops(scripts, "130", 5);
        scripts.Frame(Movie, "140", 1, clip => clip.Stop());
        scripts.Frame(Movie, "140", 60, clip => clip.GotoAndPlay(2));
        scripts.Frame(Movie, "170", 1, clip => clip.Stop());

        // Press blockers under modal panels: an empty onPress makes the clip catch the pointer.
        foreach (string blocker in new[] { "2", "51", "73", "89" })
        {
            scripts.Frame(Movie, blocker, 1, clip => clip.OnPress = static _ => { });
        }

        RegisterJobInfo(scripts);
        RegisterErrorPanel(scripts);
        RegisterLoading(scripts);
    }

    /// <summary>
    /// The host sets <c>VCTOFLASH</c> to "NAME|value"; role.dat's watcher <c>getMsg</c> dispatches it.
    /// </summary>
    public void Receive(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        string[] parts = message.Split('|');
        string name = parts[0];
        string value = parts.Length > 1 ? parts[1] : string.Empty;
        switch (name)
        {
            case "PATH":
                LoadIni();
                break;
            case "ERROR":
            case "CREATEFAIL":
                ShowError(value);
                break;
            case "EXIT":
                ShowQuitWindow();
                break;
            case "PROGRESS" when value is "1" or "80":
                Root.ChildClip("inGameMC").Play();
                break;
            case "USERACCOUNT":
                UserAccount = value;
                break;
            case "LOADING_TRACE":
                ShowLoadingTrace(value);
                break;

                // BANNED's handler is empty; HIDERETURNBTN reads an undefined variable and changes nothing.
        }
    }

    void ICharacterSelectionHost.RollOverItem(int id)
    {
        ShowInfo(Root.ChildClip("jobInfo"), id);
        _sounds.Play("snd_cs", "rollOver_char");
    }

    void ICharacterSelectionHost.PressItem(int id)
    {
        _sounds.Play("snd_cs", "click_char");
        SelectJob = id;
        ShowInfo(Root.ChildClip("jobInfo"), id);
        SetNextButtonState();
    }

    void ICharacterSelectionHost.RollOutItem(int id) => ShowInfo(Root.ChildClip("jobInfo"), SelectJob);

    private void RootFrame1(FlashClip root)
    {
        _root = root;
        root.Stop();
        SelectJob = 0;
        SelectBody = 0;
        UserName = string.Empty;
        Error = 0;

        bool enterDown = false;
        bool escapeDown = false;
        FlashClip keys = root.CreateEmptyMovieClip("keyListener", 333);
        keys.OnEnterFrame = clip =>
        {
            if (clip.Stage.IsKeyDown(EscapeKey))
            {
                if (!escapeDown)
                {
                    escapeDown = true;
                    PressEscape();
                }
            }
            else
            {
                escapeDown = false;
            }

            if (clip.Stage.IsKeyDown(EnterKey))
            {
                if (!enterDown)
                {
                    enterDown = true;
                    PressEnter();
                }
            }
            else
            {
                enterDown = false;
            }
        };
    }

    /// <summary><c>getPath</c> → <c>CIni</c> → <c>iniLoaded</c>: start the stage and load logo.swf and characterInAndOut.</summary>
    private void LoadIni()
    {
        FlashClip root = Root;
        root.Play();
        FlashClip logo = root.ChildClip("logoMC");
        if (Number(_ini.Get("logo", "xPos")) is { } x && Number(_ini.Get("logo", "yPos")) is { } y)
        {
            logo.X = x;
            logo.Y = y;
        }

        root.ChildClip("characterInAndOut").LoadMovie(_characterInAndOut);
    }

    private void RootFrame2(FlashClip root)
    {
        root.Stop();
        _sounds.Play("snd_cs", "show");
        root.OnEnterFrame = clip =>
        {
            FlashClip characterInAndOut = clip.ChildClip("characterInAndOut");
            if (CharacterInAndOutScripts.IsLoaded(characterInAndOut))
            {
                CharacterInAndOutScripts.InPlay(characterInAndOut);
                clip.OnEnterFrame = null;
            }
        };
    }

    private void RootFrame3(FlashClip root)
    {
        root.Stop();
        SelectJob = 0;
        SetNextButtonState();

        FlashClip back = root.ChildClip("returnBtn");
        back.OnRollOver = clip =>
        {
            _sounds.Play("snd_cs", "rollOver_return");
            clip.GotoAndStop(2);
        };
        back.OnRollOut = back.OnDragOut = clip => clip.GotoAndStop(1);
        back.OnPress = clip =>
        {
            _sounds.Play("snd_cs", "click_return");
            clip.GotoAndStop(3);
        };
        back.OnRelease = ReleaseBack;

        FlashClip next = root.ChildClip("nextBtn");
        next.OnRollOver = clip =>
        {
            _sounds.Play("snd_cs", "rollOver_next");
            if (clip.CurrentFrame != 5)
            {
                clip.GotoAndStop(2);
            }
            else if (root.ChildClip("flash1").CurrentFrame == 1)
            {
                root.ChildClip("flash1").GotoAndPlay(2);
            }
        };
        next.OnRollOut = next.OnDragOut = clip =>
        {
            if (clip.CurrentFrame != 5)
            {
                clip.GotoAndStop(1);
            }
            else
            {
                root.ChildClip("flash1").GotoAndStop(1);
            }
        };
        next.OnPress = clip =>
        {
            _sounds.Play("snd_cs", "click_next");
            if (clip.CurrentFrame != 5)
            {
                clip.GotoAndStop(3);
            }
        };
        next.OnRelease = clip =>
        {
            // Unqualified _currentframe and gotoAndStop resolve against the root timeline that defined the handler.
            if (SelectJob != 0 && root.CurrentFrame == 3)
            {
                root.GotoAndStop(4);
            }
            else
            {
                Error = 1;
                Root.ChildClip("errorMC").GotoAndPlay(2);
            }

            if (clip.CurrentFrame != 5)
            {
                clip.GotoAndStop(2);
            }
        };

        _pressEnterNormal = () => next.OnRelease?.Invoke(next);
        _pressEscNormal = () => back.OnRelease?.Invoke(back);
    }

    private void RootFrame4(FlashClip root)
    {
        root.Stop();
        _sounds.Play("snd_cs", "rollOver_char");
        CharacterInAndOutScripts.OutPlay(root.ChildClip("characterInAndOut"));
        root.ChildClip("nextBtn").OnRelease = static _ => { };
    }

    private void ReleaseBack(FlashClip back)
    {
        FlashClip root = back.Parent ?? Root;
        if (root.CurrentFrame == 5)
        {
            root.GotoAndStop(2);
        }
        else if (root.CurrentFrame == 3)
        {
            _commands.Command("CMD_DRIVERBACK", string.Empty);
        }

        back.GotoAndStop(2);
    }

    private void SetNextButtonState()
    {
        if (Root.Child("nextBtn") is not FlashClip next)
        {
            return;
        }

        if (SelectJob != 0)
        {
            next.GotoAndStop(5);
        }
        else if (next.CurrentFrame == 5)
        {
            next.GotoAndStop(1);
        }
    }

    private void PressEnter()
    {
        if (Root.ChildClip("inGameMC").Get<bool>("isLoading"))
        {
            return;
        }

        // role.dat flips the panel flag on every Enter, whichever handler ran.
        if (!IsErrorPanelShown)
        {
            _pressEnterNormal();
            IsErrorPanelShown = true;
        }
        else
        {
            _pressEnterError();
            IsErrorPanelShown = false;
        }
    }

    private void PressEscape()
    {
        if (Root.ChildClip("inGameMC").Get<bool>("isLoading"))
        {
            return;
        }

        if (!IsErrorPanelShown)
        {
            _pressEscNormal();
        }
        else
        {
            _pressEscError();
        }
    }

    private void RegisterJobInfo(FlashScriptTable scripts)
    {
        scripts.Frame(Movie, "116", 1, clip =>
        {
            clip.Set("infoID", 0);
            clip.Stop();
        });
        scripts.Frame(Movie, "116", 3, clip =>
        {
            int id = clip.Get<int>("infoID");
            if (id is >= 1 and <= 12)
            {
                clip.ChildText("infoTxt").Text = TurnToNewline(_ini.Get("info", $"info{(id - 1) / 2}") ?? string.Empty);
            }
        });
        scripts.Frame(Movie, "116", 7, clip => clip.Stop());
        scripts.Frame(Movie, "116", 12, clip => clip.GotoAndPlay(2));
    }

    /// <summary><c>jobInfo.showInfo(id)</c>.</summary>
    private static void ShowInfo(FlashClip jobInfo, int id)
    {
        jobInfo.Set("infoID", id);
        if (jobInfo.CurrentFrame == 7)
        {
            jobInfo.GotoAndPlay(8);
        }
        else
        {
            jobInfo.GotoAndPlay(2);
        }
    }

    private void RegisterErrorPanel(FlashScriptTable scripts)
    {
        scripts.Frame(Movie, "77", 1, clip => clip.Stop());
        scripts.Frame(Movie, "77", 2, clip =>
        {
            clip.ChildClip("maskMC").OnPress = static _ => { };
            SetStageButtons(frame: 4, enabled: false);
            _sounds.Play("snd_db", "show");
            IsErrorPanelShown = true;
        });
        scripts.Frame(Movie, "77", 6, clip =>
        {
            clip.Stop();
            clip.ChildText("txt").Text = Error switch
            {
                0 => ErrorText,
                1 => "Please select your profession!",
                2 => "Please select your body size.",
                3 => "Please enter your First Name!",
                _ => clip.ChildText("txt").Text,
            };

            FlashButtonInstance ok = (FlashButtonInstance)(clip.Child("okBtn") ?? throw new InvalidOperationException("errorMC has no okBtn."));
            ok.OnRollOver = _ => _sounds.Play("snd_db", "rollOver_OK");
            ok.OnPress = _ => _sounds.Play("snd_db", "click_OK");
            ok.OnRelease = _ => clip.Play();
            _pressEnterError = () => clip.Play();
            _pressEscError = () => clip.Play();
        });
        scripts.Frame(Movie, "77", 7, _ => _sounds.Play("snd_db", "hide"));
        scripts.Frame(Movie, "77", 10, clip =>
        {
            SetStageButtons(frame: 1, enabled: true);
            IsErrorPanelShown = false;
            clip.GotoAndStop(1);
        });
    }

    /// <summary>Moves returnBtn, createBtn and nextBtn (whichever this stage has) to a frame and sets <c>enabled</c>.</summary>
    private void SetStageButtons(int frame, bool enabled)
    {
        foreach (string name in new[] { "returnBtn", "createBtn", "nextBtn" })
        {
            if (Root.Child(name) is FlashClip button)
            {
                button.GotoAndStop(frame);
                button.Enabled = enabled;
            }
        }
    }

    private void ShowError(string text)
    {
        Error = 0;
        ErrorText = text;
        Root.ChildClip("errorMC").GotoAndPlay(2);
        Root.ChildClip("inGameMC").GotoAndStop(1);
    }

    private void RegisterLoading(FlashScriptTable scripts)
    {
        scripts.Frame(Movie, "110", 1, clip =>
        {
            clip.Stop();
            clip.Set("isLoading", false);
            clip.Visible = false;
        });
        scripts.Frame(Movie, "110", 2, clip =>
        {
            clip.Set("isLoading", true);
            clip.Visible = true;
        });
        scripts.Frame(Movie, "110", 71, clip =>
        {
            clip.Stop();
            _commands.Command("CMD_GAMESTART", "100");
        });
        scripts.Frame(Movie, "110", 201, clip =>
        {
            clip.Stop();
            _commands.Command("CMD_CLOSE", "1");
        });
    }

    /// <summary><c>inGameMC.getLoadingTace(text)</c>.</summary>
    private void ShowLoadingTrace(string text)
    {
        FlashClip loading = Root.ChildClip("inGameMC");
        loading.Visible = true;
        loading.ChildText("txtMsg1").Text = text;
    }

    private static void Stops(FlashScriptTable scripts, string timeline, int frames)
    {
        for (int frame = 1; frame <= frames; frame++)
        {
            scripts.Frame(Movie, timeline, frame, static clip => clip.Stop());
        }
    }

    /// <summary>role.dat <c>turnToNewline</c>: a literal <c>\n</c> becomes a line break and <c>~</c> a space.</summary>
    internal static string TurnToNewline(string text) => text.Replace("\\n", "\n", StringComparison.Ordinal).Replace('~', ' ');

    /// <summary>AS2 <c>Number(value)</c>, or null where Flash would get NaN (and so ignore the assignment).</summary>
    private static float? Number(string? value) =>
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float number) && float.IsFinite(number) ? number : null;
}
