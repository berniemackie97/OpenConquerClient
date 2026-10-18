using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;

namespace OpenConquer.Launcher.Play;

/// <summary>
/// The retail <c>play.exe</c> dialog drawn at its native 800x600: background, foreground pictures, version text,
/// the start buttons, the three bottom buttons, the centre web panel and the Low Definition confirmation.
/// </summary>
internal sealed class PlayScreen : Canvas, IDisposable
{
    // Control 1009 TextRect colour 0x7F and control 1012 colour 0xFF are COLORREFs: dark red and red.
    private static readonly IBrush s_hintTextBrush = new SolidColorBrush(Color.FromRgb(0x7F, 0x00, 0x00));
    private static readonly IBrush s_versionTextBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0x00));

    // The dialog font is 9pt 宋体 (SimSun); PlayGui.ini sets each text's cell height and average width in pixels.
    private static readonly FontFamily s_dialogFont = new("SimSun, NSimSun, Songti SC, STSong, Noto Serif CJK SC, serif");

    private readonly List<IDisposable> _owned = [];
    private readonly Dictionary<PlayButton, PlaySkinButton> _buttons = [];
    private readonly Image _hint;
    private readonly StackPanel _hintText;
    private readonly TextBlock _version;
    private readonly Control _webPanel;

    public PlayScreen(Control webPanel)
    {
        _webPanel = webPanel ?? throw new ArgumentNullException(nameof(webPanel));
        Width = PlayLayout.Window.Width;
        Height = PlayLayout.Window.Height;
        Styles.Add(CreateTooltipStyle());

        try
        {
            Image background = AddImage(PlayArtwork.Main, new Point(0, 0));
            background.IsHitTestVisible = true;
            background.PointerPressed += (_, e) => BackgroundPressed?.Invoke(this, e);

            AddImage(PlayArtwork.Side, PlayLayout.Side);
            AddImage(PlayArtwork.VersionLabel, PlayLayout.VersionLabel);
            AddImage(PlayArtwork.Logo, PlayLayout.Logo);

            _version = new TextBlock
            {
                FontFamily = s_dialogFont,
                FontSize = 15,
                Foreground = s_versionTextBrush,
                RenderTransformOrigin = RelativePoint.TopLeft,
                // FontWidth=6 against SimSun's 7.5-pixel half-width cell at a 15-pixel height.
                RenderTransform = new ScaleTransform(0.8, 1),
                IsHitTestVisible = false,
            };
            Place(_version, PlayLayout.VersionText);

            Place(webPanel, PlayLayout.WebPanel.Position);
            webPanel.Width = PlayLayout.WebPanel.Width;
            webPanel.Height = PlayLayout.WebPanel.Height;

            foreach (PlayButton button in (ReadOnlySpan<PlayButton>)[PlayButton.High, PlayButton.Standard, PlayButton.Low, PlayButton.Exit, PlayButton.Site, PlayButton.Register])
            {
                AddButton(button);
            }

            _hint = AddImage(PlayArtwork.Hint, PlayLayout.Hint);
            _hintText = new StackPanel
            {
                IsHitTestVisible = false,
                ClipToBounds = true,
                Width = PlayArtwork.Hint.Width - PlayLayout.HintText.X,
                Height = PlayArtwork.Hint.Height - PlayLayout.HintText.Y,
            };
            Place(_hintText, PlayLayout.Hint + PlayLayout.HintText);
            AddButton(PlayButton.Continue);
            AddButton(PlayButton.Return);
            HideHint();
        }
        catch
        {
            Dispose();
            throw;
        }

        ToolTip.SetTip(_buttons[PlayButton.High], PlayText.HighTip);
        ToolTip.SetTip(_buttons[PlayButton.Standard], PlayText.StandardTip);
        ToolTip.SetTip(_buttons[PlayButton.Low], PlayText.LowTip);
    }

    /// <summary>A start, popup or bottom button was clicked.</summary>
    public event EventHandler<PlayButton>? ButtonClicked;

    /// <summary>The background was pressed: the native dialog moves with a drag from anywhere outside its controls.</summary>
    public event EventHandler<PointerPressedEventArgs>? BackgroundPressed;

    public bool IsHintVisible => _hint.IsVisible;

    /// <summary>The most-used start mode shows its normal art; the other two show variant 1.</summary>
    public void ShowUsage(GameModeUsage usage)
    {
        GameMode recommended = usage.MostUsed;
        _buttons[PlayButton.High].Faded = recommended != GameMode.High;
        _buttons[PlayButton.Standard].Faded = recommended != GameMode.Standard;
        _buttons[PlayButton.Low].Faded = recommended != GameMode.Low;
    }

    public void ShowVersion(string? version) => _version.Text = version;

    public void SetStartEnabled(bool enabled)
    {
        _buttons[PlayButton.High].IsEnabled = enabled;
        _buttons[PlayButton.Standard].IsEnabled = enabled;
        _buttons[PlayButton.Low].IsEnabled = enabled;
    }

    /// <summary>Shows control 1009 with lines of text and the given buttons (Continue, Return or both).</summary>
    public void ShowHint(IEnumerable<string> lines, params PlayButton[] buttons)
    {
        ArgumentNullException.ThrowIfNull(lines);
        _hintText.Children.Clear();
        foreach (string line in lines)
        {
            // Hint TextRect: FontHeight=20, FontWidth=10, LineHeight defaults to the font height.
            _hintText.Children.Add(CreateHintLine(line, double.NaN));
        }

        // The web panel is a native view that always draws above the dialog's own controls, so it steps
        // aside while the hint covers it.
        _webPanel.IsVisible = false;
        _hint.IsVisible = true;
        _hintText.IsVisible = true;
        foreach (PlayButton button in (ReadOnlySpan<PlayButton>)[PlayButton.Continue, PlayButton.Return])
        {
            _buttons[button].IsVisible = buttons.Contains(button);
        }

        if (buttons.Length > 0)
        {
            _buttons[buttons[0]].Focus();
        }
    }

    /// <summary>Shows control 1009 with a heading and a wrapped explanation, in the hint's text style.</summary>
    public void ShowHintMessage(string title, string detail, params PlayButton[] buttons)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(detail);
        ShowHint([], buttons);
        double width = PlayArtwork.Hint.Width - 2 * PlayLayout.HintText.X;
        _hintText.Children.Add(CreateHintLine(title, width));
        _hintText.Children.Add(CreateHintLine(string.Empty, width));
        _hintText.Children.Add(CreateHintLine(detail, width));
    }

    public void HideHint()
    {
        _hint.IsVisible = false;
        _hintText.IsVisible = false;
        _buttons[PlayButton.Continue].IsVisible = false;
        _buttons[PlayButton.Return].IsVisible = false;
        _webPanel.IsVisible = true;
    }

    public void Dispose()
    {
        foreach (IDisposable owned in _owned)
        {
            owned.Dispose();
        }

        _owned.Clear();
    }

    /// <summary>
    /// play.exe has no comctl32 v6 manifest, so its tooltips are the classic system ones: info-background yellow,
    /// black border and text, <c>TTM_SETMAXTIPWIDTH</c> 2000 (<c>Play.dat [Tip] MaxWidth</c>).
    /// </summary>
    private static Style CreateTooltipStyle() => new(selector => selector.OfType<ToolTip>())
    {
        Setters =
        {
            new Setter(TemplatedControl.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xE1))),
            new Setter(TemplatedControl.ForegroundProperty, Brushes.Black),
            new Setter(TemplatedControl.BorderBrushProperty, Brushes.Black),
            new Setter(TemplatedControl.BorderThicknessProperty, new Thickness(1)),
            new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(0)),
            new Setter(TemplatedControl.PaddingProperty, new Thickness(2, 1)),
            new Setter(TemplatedControl.FontFamilyProperty, new FontFamily("Tahoma, Segoe UI, sans-serif")),
            new Setter(TemplatedControl.FontSizeProperty, 11d),
            new Setter(MaxWidthProperty, 2000d),
        },
    };

    private static TextBlock CreateHintLine(string text, double width) => new()
    {
        Text = text,
        Width = width,
        TextWrapping = double.IsNaN(width) ? TextWrapping.NoWrap : TextWrapping.Wrap,
        FontFamily = s_dialogFont,
        FontSize = 20,
        LineHeight = 20,
        Foreground = s_hintTextBrush,
        // TextOut drew each line unclipped; only the hint control's own edge cut long lines.
        ClipToBounds = false,
    };

    private Image AddImage(PlayImage artwork, Point position)
    {
        PlayBitmap bitmap = PlayBitmap.Load(artwork);
        _owned.Add(bitmap);
        Image image = new()
        {
            Source = bitmap.Bitmap,
            Width = bitmap.Width,
            Height = bitmap.Height,
            IsHitTestVisible = false,
        };
        Place(image, position);
        return image;
    }

    private void AddButton(PlayButton button)
    {
        PlaySkinButton control = new(button);
        _owned.Add(control);
        _buttons.Add(button, control);
        control.Click += (_, _) => ButtonClicked?.Invoke(this, button);
        AutomationProperties.SetName(control, AccessibleName(button));
        ToolTip.SetShowDelay(control, 500);
        Place(control, PlayLayout.Of(button));
    }

    // The text painted on each button's art.
    private static string AccessibleName(PlayButton button) => button switch
    {
        PlayButton.High => "High Definition - Start",
        PlayButton.Standard => "Standard Definition - Start",
        PlayButton.Low => "Low Definition - Start",
        PlayButton.Continue => "Continue",
        PlayButton.Return => "Cancel",
        PlayButton.Exit => "Exit",
        PlayButton.Site => "Site",
        PlayButton.Register => "Register",
        _ => throw new ArgumentOutOfRangeException(nameof(button), button, "Unknown play button."),
    };

    private void Place(Control control, Point position)
    {
        SetLeft(control, position.X);
        SetTop(control, position.Y);
        Children.Add(control);
    }
}
