namespace OpenConquer.Client.Tests;

public sealed class ClientWindowCreationSequenceTests
{
    /// <summary>
    /// The splash is torn down as soon as initialization returns. Retail hides and destroys the
    /// startup logo at <c>0x5AF4E7</c> and <c>0x5AF4F2</c> with no minimum display duration, so
    /// there is no completion step between initialization and disposal.
    /// </summary>
    [Fact]
    public void CreateMainAfterStartup_DestroysStartupSplashBeforeConstructingMainWindow()
    {
        List<string> events = [];
        RecordingStartupSplash startupSplash = new(events);

        object mainWindow = ClientWindowCreationSequence.CreateMainAfterStartup(
            startupSplash,
            () => events.Add("runtime-initialized"),
            () =>
            {
                events.Add("main-created");
                return new object();
            }
        );

        Assert.NotNull(mainWindow);
        Assert.Equal(
            ["startup-shown", "runtime-initialized", "startup-disposed", "main-created"],
            events
        );
    }

    [Fact]
    public void CreateMainAfterStartup_DestroysStartupSplashWhenPresentationFails()
    {
        List<string> events = [];
        RecordingStartupSplash startupSplash = new(events, throwWhenShown: true);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            ClientWindowCreationSequence.CreateMainAfterStartup(
                startupSplash,
                () => events.Add("runtime-initialized"),
                () =>
                {
                    events.Add("main-created");
                    return new object();
                }
            )
        );

        Assert.Equal("Startup presentation failed.", exception.Message);
        Assert.Equal(["startup-shown", "startup-disposed"], events);
    }

    [Fact]
    public void CreateMainAfterStartup_DestroysStartupSplashWithoutConstructingMainWhenInitializationFails()
    {
        List<string> events = [];
        RecordingStartupSplash startupSplash = new(events);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            ClientWindowCreationSequence.CreateMainAfterStartup(
                startupSplash,
                () =>
                {
                    events.Add("runtime-initialized");
                    throw new InvalidOperationException("Runtime initialization failed.");
                },
                () =>
                {
                    events.Add("main-created");
                    return new object();
                }
            )
        );

        Assert.Equal("Runtime initialization failed.", exception.Message);
        Assert.Equal(["startup-shown", "runtime-initialized", "startup-disposed"], events);
    }

    [Fact]
    public void CreateMainAfterStartup_RejectsNullArguments()
    {
        List<string> events = [];

        Assert.Throws<ArgumentNullException>(
            () => ClientWindowCreationSequence.CreateMainAfterStartup<object>(null!, () => { }, () => new object())
        );

        Assert.Throws<ArgumentNullException>(
            () => ClientWindowCreationSequence.CreateMainAfterStartup(new RecordingStartupSplash(events), null!, () => new object())
        );

        Assert.Throws<ArgumentNullException>(
            () => ClientWindowCreationSequence.CreateMainAfterStartup<object>(new RecordingStartupSplash(events), () => { }, null!)
        );
    }

    private sealed class RecordingStartupSplash : IStartupSplash
    {
        private readonly List<string> _events;
        private readonly bool _throwWhenShown;

        public RecordingStartupSplash(List<string> events, bool throwWhenShown = false)
        {
            _events = events;
            _throwWhenShown = throwWhenShown;
        }

        public void Show()
        {
            _events.Add("startup-shown");

            if (_throwWhenShown)
            {
                throw new InvalidOperationException("Startup presentation failed.");
            }
        }

        public void Dispose()
        {
            _events.Add("startup-disposed");
        }
    }
}
