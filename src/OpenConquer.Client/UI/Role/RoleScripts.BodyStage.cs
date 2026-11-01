using System.Globalization;
using OpenConquer.Client.UI.Flash;

namespace OpenConquer.Client.UI.Role;

/// <summary>role.dat's body and name stage (root frames 5–6, <c>sb</c>), confirm panel and quit windows.</summary>
internal sealed partial class RoleScripts
{
    private const int MaximumNameBytes = 15;

    // Per-class sprites of sb (frames 37–48) and createBtn, which only stop on each of their frames.
    private static readonly string[] s_threeFrameButtons =
    [
        "199", "206", "234", "242", "246", "247", "248", "249", "253", "255", "259", "260", "264", "265", "269", "270", "274", "275",
        "279", "280", "284", "285", "289", "290", "297", "298", "302", "303",
    ];

    private static readonly (string Sprite, int Frame)[] s_lastFrameStops =
    [
        ("230", 8), ("233", 7), ("240", 8), ("241", 7), ("245", 8), ("252", 8), ("254", 8), ("258", 8), ("263", 8), ("268", 8), ("273", 8),
        ("278", 8), ("283", 8), ("288", 8), ("296", 8), ("301", 8),
    ];

    private bool _createWindowOpen;
    private int _lastKeySound = -1;

    private void RegisterBodyStage(FlashScriptTable scripts)
    {
        scripts.Frame(Movie, "root", 5, RootFrame5);
        scripts.Frame(Movie, "root", 6, root => root.Stop());

        foreach (string sprite in s_threeFrameButtons)
        {
            Stops(scripts, sprite, 3);
        }

        Stops(scripts, "312", 5);
        foreach ((string sprite, int frame) in s_lastFrameStops)
        {
            scripts.Frame(Movie, sprite, frame, static clip => clip.Stop());
        }

        scripts.Frame(Movie, "316", 1, clip => clip.Stop());
        scripts.Frame(Movie, "316", 60, clip => clip.GotoAndPlay(2));

        RegisterNameAndBody(scripts);
        RegisterConfirmPanel(scripts);
        RegisterQuitWindow(scripts, "75");
        RegisterQuitWindow(scripts, "112");
    }

    private void RootFrame5(FlashClip root)
    {
        root.Stop();
        SelectBody = 0;
        UserName = string.Empty;
        _createWindowOpen = false;

        root.OnEnterFrame = clip =>
        {
            if (_createWindowOpen || clip.Child("createBtn") is not FlashClip create)
            {
                return;
            }

            FlashClip body = clip.ChildClip("sb");
            if (body.ChildText("userNameTxt").Text.Length != 0 && body.Get<int>("nSelectBody") > 0)
            {
                create.GotoAndStop(5);
            }
            else if (create.CurrentFrame == 5)
            {
                create.GotoAndStop(1);
            }
        };

        FlashClip createButton = root.ChildClip("createBtn");
        createButton.OnRollOver = clip =>
        {
            _sounds.Play("snd_bs", "rollOver_create");
            if (clip.CurrentFrame != 5)
            {
                clip.GotoAndStop(2);
            }
            else if (root.ClipOrNull("flash2") is { CurrentFrame: 1 } flash)
            {
                flash.GotoAndPlay(2);
            }
        };
        createButton.OnRollOut = createButton.OnDragOut = clip =>
        {
            if (clip.CurrentFrame != 5)
            {
                clip.GotoAndStop(1);
            }
            else
            {
                root.ClipOrNull("flash2")?.GotoAndStop(1);
            }
        };
        createButton.OnPress = clip =>
        {
            _sounds.Play("snd_bs", "click_create");
            clip.GotoAndStop(3);
        };
        createButton.OnRelease = clip =>
        {
            FlashClip body = root.ChildClip("sb");
            string name = body.ChildText("userNameTxt").Text;
            int size = body.Get<int>("nSelectBody");
            if (name.Length != 0 && size > 0)
            {
                _createWindowOpen = true;
                clip.OnRollOut?.Invoke(clip);
                UserName = name;
                SelectBody = size;
                root.ChildClip("createCharacterMC").GotoAndPlay(2);
            }
            else if (name.Length == 0)
            {
                Error = 3;
                root.ChildClip("errorMC").GotoAndPlay(2);
            }
            else
            {
                Error = 2;
                root.ChildClip("errorMC").GotoAndPlay(2);
            }

            clip.GotoAndStop(2);
        };

        FlashClip back = root.ChildClip("returnBtn");
        _pressEnterNormal = () => createButton.OnRelease?.Invoke(createButton);
        _pressEscNormal = () => back.OnRelease?.Invoke(back);
    }

    private void RegisterNameAndBody(FlashScriptTable scripts)
    {
        scripts.Frame(Movie, "304", 1, body =>
        {
            body.Set("nSelectBody", 0);
            body.ChildText("infoTxt").Text = BodyInfo();
            _lastKeySound = -1;

            FlashTextInstance name = body.ChildText("userNameTxt");
            name.OnChanged = NameChanged;
            name.AcceptInsertion = static value => ByteLength(value) <= MaximumNameBytes;
            name.Visible = false;
            body.ChildClip("bb1").GotoAndStop(SelectJob);
            body.ChildClip("sb1").GotoAndStop(SelectJob);
        });
        scripts.Frame(Movie, "304", 2, _ => _sounds.Play("snd_bs", "show"));
        scripts.Frame(Movie, "304", 36, body =>
        {
            body.ChildText("userNameTxt").Visible = true;
            body.Set("nSelectBody", 0);
            body.GotoAndStop(body.CurrentFrame + SelectJob);
            body.Stage.SetFocus(body.ChildText("userNameTxt"));
        });

        for (int frame = 37; frame <= 48; frame++)
        {
            scripts.Frame(Movie, "304", frame, InitBodyButtons);
        }

        scripts.Frame(Movie, "350", 1, body =>
        {
            body.ChildClip("bb1").GotoAndStop(SelectJob);
            body.ChildClip("sb1").GotoAndStop(SelectJob);
            body.ChildText("infoTxt").Text = BodyInfo();
        });
        scripts.Frame(Movie, "350", 2, _ => _sounds.Play("snd_bs", "hide"));
        scripts.Frame(Movie, "350", 26, body =>
        {
            body.Stop();
            Root.GotoAndStop(2);
        });
    }

    private string BodyInfo() =>
        SelectJob is >= 1 and <= 12 ? TurnToNewline(_ini.Get("info", $"infoII{(SelectJob - 1) / 2}") ?? string.Empty) : string.Empty;

    /// <summary>
    /// <c>userNameTxt.onChanged</c>: plays one of three key sounds, never repeating the previous sound.
    /// </summary>
    private void NameChanged(FlashTextInstance name)
    {
        int sound;
        do
        {
            sound = Random.Shared.Next(3);
        }
        while (sound == _lastKeySound);

        _lastKeySound = sound;
        _sounds.Play("snd_bs", $"keyboardDown_{sound}");
    }

    private static int ByteLength(string text)
    {
        int length = 0;
        foreach (char character in text)
        {
            length += character > 128 ? 2 : 1;
        }

        return length;
    }

    /// <summary><c>initBtn()</c> on the class frame of sb: wires that frame's size buttons and silhouettes.</summary>
    private void InitBodyButtons(FlashClip body)
    {
        FlashClip big = body.ChildClip("bodySizeBtn_0");
        FlashClip small = body.ChildClip("bodySizeBtn_1");
        FlashClip bigFigure = body.ChildClip("sb");
        FlashClip smallFigure = body.ChildClip("sm");

        big.OnRelease = _ => SelectSize(body, 1);
        small.OnRelease = _ => SelectSize(body, 2);
        big.OnPress = static _ => { };
        small.OnPress = static _ => { };
        big.OnRollOver = small.OnRollOver = clip =>
        {
            clip.GotoAndStop(2);
            _sounds.Play("snd_bs", "rollOver_create");
        };
        big.OnRollOut = big.OnDragOut = clip => clip.GotoAndStop(body.Get<int>("nSelectBody") == 1 ? 3 : 1);
        small.OnRollOut = small.OnDragOut = clip => clip.GotoAndStop(body.Get<int>("nSelectBody") == 2 ? 3 : 1);

        bigFigure.OnRollOver = clip =>
        {
            if (body.Get<int>("nSelectBody") != 1 && clip.CurrentFrame != 3)
            {
                _sounds.Play("snd_bs", "rollOver_big");
                clip.GotoAndStop(2);
            }
        };
        bigFigure.OnRollOut = clip =>
        {
            if (body.Get<int>("nSelectBody") != 1 && clip.CurrentFrame != 3)
            {
                clip.GotoAndStop(1);
            }
        };
        bigFigure.OnPress = _ => SelectSize(body, 1);
        smallFigure.OnRollOver = clip =>
        {
            if (body.Get<int>("nSelectBody") != 2 && clip.CurrentFrame != 3)
            {
                _sounds.Play("snd_bs", "rollOver_small");
                clip.GotoAndStop(2);
            }
        };
        smallFigure.OnRollOut = clip =>
        {
            if (body.Get<int>("nSelectBody") != 2 && clip.CurrentFrame != 3)
            {
                clip.GotoAndStop(1);
            }
        };
        smallFigure.OnPress = _ => SelectSize(body, 2);
    }

    /// <summary><c>selectSize(n)</c>: 1 big, 2 small.</summary>
    private void SelectSize(FlashClip body, int size)
    {
        FlashClip bigFigure = body.ChildClip("sb");
        FlashClip smallFigure = body.ChildClip("sm");
        if (size == 1 && body.Get<int>("nSelectBody") != 1 && bigFigure.CurrentFrame != 3)
        {
            _sounds.Play("snd_bs", "click_big");
            body.Set("nSelectBody", 1);
            smallFigure.GotoAndStop(1);
            bigFigure.GotoAndStop(3);
            body.ChildClip("bodySizeBtn_0").GotoAndStop(3);
            body.ChildClip("bodySizeBtn_1").GotoAndStop(1);
        }
        else if (size == 2 && body.Get<int>("nSelectBody") != 2 && smallFigure.CurrentFrame != 3)
        {
            _sounds.Play("snd_bs", "click_small");
            body.Set("nSelectBody", 2);
            smallFigure.GotoAndStop(3);
            bigFigure.GotoAndStop(1);
            body.ChildClip("bodySizeBtn_0").GotoAndStop(1);
            body.ChildClip("bodySizeBtn_1").GotoAndStop(3);
        }
    }

    private void RegisterConfirmPanel(FlashScriptTable scripts)
    {
        scripts.Frame(Movie, "58", 1, panel =>
        {
            panel.Stop();
            panel.Set("nType", -1);
        });
        scripts.Frame(Movie, "58", 2, panel =>
        {
            SetButton("returnBtn", frame: 4, enabled: false);
            SetButton("createBtn", frame: 4, enabled: false);
            _sounds.Play("snd_db", "show");
            panel.Stage.SetFocus(null);
            IsErrorPanelShown = true;
        });
        scripts.Frame(Movie, "58", 6, ConfirmPanelFrame6);
        scripts.Frame(Movie, "58", 7, _ => _sounds.Play("snd_db", "hide"));
        scripts.Frame(Movie, "58", 10, panel =>
        {
            IsErrorPanelShown = false;
            if (panel.Get<int>("nType") == 0)
            {
                panel.GotoAndStop(1);
                SetButton("returnBtn", frame: 1, enabled: true);
                SetButton("createBtn", frame: 1, enabled: true);
                _createWindowOpen = false;
            }
        });

        scripts.Frame(Movie, "57", 1, warning =>
        {
            FlashButtonInstance ok = Button(warning, "okBtn");
            FlashButtonInstance cancel = Button(warning, "cancelBtn");
            ok.OnRollOver = _ => _sounds.Play("snd_db", "rollOver_OK");
            ok.OnPress = _ => _sounds.Play("snd_db", "click_OK");
            cancel.OnRollOver = _ => _sounds.Play("snd_db", "rollOver_cancel");
            cancel.OnPress = _ => _sounds.Play("snd_db", "click_cancel");
            ok.OnRelease = _ =>
            {
                warning.Visible = false;
                SubmitCreation(warning.Parent ?? throw new InvalidOperationException("mcSafe has no panel."));
                HideWarning(warning);
            };
            cancel.OnRelease = _ => HideWarning(warning);
            warning.Visible = false;
        });
    }

    private void ConfirmPanelFrame6(FlashClip panel)
    {
        panel.Stop();
        panel.ChildText("userNameTxt").Text = UserName;

        // SWF 6 ActionScript is case-insensitive, so the panel's headMC.gotoAndstop(job) does run.
        panel.ChildClip("headMC").GotoAndStop(SelectJob);
        bool female = SelectJob % 2 == 0;
        if (SelectJob is >= 1 and <= 12)
        {
            panel.ChildText("jobTxt").Text = _ini.Get("affirmPanel", $"vocation{SelectJob - 1}") ?? string.Empty;
        }

        string? shape = SelectBody switch
        {
            2 => female ? "shape3" : "shape1",
            1 => female ? "shape2" : "shape0",
            _ => null,
        };
        if (shape is not null)
        {
            panel.ChildText("bodyTxt").Text = _ini.Get("affirmPanel", shape) ?? string.Empty;
        }

        FlashButtonInstance ok = Button(panel, "okBtn");
        FlashButtonInstance cancel = Button(panel, "cancelBtn");
        FlashClip warning = panel.ChildClip("mcSafe");
        ok.OnRelease = _ =>
        {
            if (UserAccount == UserName)
            {
                ShowWarning(warning);
            }
            else
            {
                SubmitCreation(panel);
            }
        };
        cancel.OnRelease = _ =>
        {
            panel.Set("nType", 0);
            panel.Play();
        };
        ok.OnRollOver = _ => _sounds.Play("snd_db", "rollOver_OK");
        ok.OnPress = _ => _sounds.Play("snd_db", "click_OK");
        cancel.OnRollOver = _ => _sounds.Play("snd_db", "rollOver_cancel");
        cancel.OnPress = _ => _sounds.Play("snd_db", "click_cancel");
        _pressEnterError = () => ok.OnRelease?.Invoke(ok);
        _pressEscError = () => cancel.OnRelease?.Invoke(cancel);
    }

    /// <summary>createCharacterMC <c>submit()</c>: role's <c>submit()</c>, the loading clip, and the panel closes.</summary>
    private void SubmitCreation(FlashClip panel)
    {
        panel.Set("nType", 1);
        _commands.Command("CMD_NICK", UserName);
        _commands.Command("CMD_BODYSIZE", (SelectBody - 1).ToString(CultureInfo.InvariantCulture));
        _commands.Command("CMD_VOCATION", (SelectJob - 1).ToString(CultureInfo.InvariantCulture));
        _commands.Command("CMD_ENTERGAME", "1");
        Root.ChildClip("inGameMC").GotoAndPlay(2);
        panel.Play();
    }

    /// <summary>mcSafe <c>show()</c>: Enter and Escape now answer the warning.</summary>
    private void ShowWarning(FlashClip warning)
    {
        _sounds.Play("snd_db", "show");
        warning.Visible = true;
        FlashButtonInstance ok = Button(warning, "okBtn");
        FlashButtonInstance cancel = Button(warning, "cancelBtn");
        _pressEnterError = () => ok.OnRelease?.Invoke(ok);
        _pressEscError = () => cancel.OnRelease?.Invoke(cancel);
    }

    /// <summary>mcSafe <c>hide()</c>: Enter and Escape answer the confirm panel again.</summary>
    private void HideWarning(FlashClip warning)
    {
        _sounds.Play("snd_db", "hide");
        warning.Visible = false;
        FlashClip panel = warning.Parent ?? throw new InvalidOperationException("mcSafe has no panel.");
        FlashButtonInstance ok = Button(panel, "okBtn");
        FlashButtonInstance cancel = Button(panel, "cancelBtn");
        _pressEnterError = () => ok.OnRelease?.Invoke(ok);
        _pressEscError = () => cancel.OnRelease?.Invoke(cancel);
    }

    private void RegisterQuitWindow(FlashScriptTable scripts, string sprite)
    {
        scripts.Frame(Movie, sprite, 1, window =>
        {
            window.Stop();
            window.Set("isShow", false);
        });
        scripts.Frame(Movie, sprite, 2, window =>
        {
            window.ChildClip("maskMC").OnPress = static _ => { };
            _sounds.Play("snd_db", "show");
            IsErrorPanelShown = true;
            window.Stage.SetFocus(null);
            window.Set("isShow", true);
        });
        scripts.Frame(Movie, sprite, 6, window =>
        {
            window.Stop();
            FlashButtonInstance ok = Button(window, "okBtn");
            FlashButtonInstance cancel = Button(window, "cancelBtn");
            cancel.OnRelease = _ => window.Play();
            ok.OnRelease = _ =>
            {
                _commands.Command("CMD_EXIT", string.Empty);
                window.Play();
            };
            ok.OnRollOver = _ => _sounds.Play("snd_db", "rollOver_OK");
            ok.OnPress = _ => _sounds.Play("snd_db", "click_OK");
            cancel.OnRollOver = _ => _sounds.Play("snd_db", "rollOver_cancel");
            cancel.OnPress = _ => _sounds.Play("snd_db", "click_cancel");
            _pressEnterError = () => ok.OnRelease?.Invoke(ok);
            _pressEscError = () => cancel.OnRelease?.Invoke(cancel);
        });
        scripts.Frame(Movie, sprite, 7, _ => _sounds.Play("snd_db", "hide"));
        scripts.Frame(Movie, sprite, 10, window =>
        {
            // Frame 10 also hides start.dat's enterGameMC masks, which role.dat does not have.
            IsErrorPanelShown = false;
            window.GotoAndStop(1);
        });
    }

    /// <summary><c>getExit()</c>: the loading quit window while loading, otherwise the stage quit window.</summary>
    private void ShowQuitWindow()
    {
        FlashClip window = Root.ChildClip(Root.ChildClip("inGameMC").Get<bool>("isLoading") ? "quitWindow2" : "quitWindow");
        if (!window.Get<bool>("isShow"))
        {
            window.Play();
        }
    }

    private void SetButton(string name, int frame, bool enabled)
    {
        if (Root.Child(name) is FlashClip button)
        {
            button.GotoAndStop(frame);
            button.Enabled = enabled;
        }
    }

    private static FlashButtonInstance Button(FlashClip clip, string name) =>
        clip.Child(name) as FlashButtonInstance ?? throw new InvalidOperationException($"Clip '{clip.Name}' has no button '{name}'.");
}
