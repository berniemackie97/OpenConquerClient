namespace OpenConquer.Client.Tests;

public sealed class ClientWindowCreationSequenceTests
{
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
            ["startup-shown", "runtime-initialized", "startup-completed", "startup-disposed", "main-created"],
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

        public void Complete()
        {
            _events.Add("startup-completed");
        }
    }
}
