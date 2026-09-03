using System.Diagnostics.CodeAnalysis;
using OpenConquer.Content;
using OpenConquer.Content.Startup;

namespace OpenConquer.Client.Tests;

public sealed class OpenGLStartupSplashTests
{
    [Fact]
    public void MissingLogo_ShowsNoWindowAndRemainsNonFatal()
    {
        StartupLogo logo = StartupLogo.Load(
            new MissingContentSource(),
            monotonicTickMilliseconds: 0
        );

        Assert.Null(logo.Image);
        Assert.NotNull(logo.UnavailableReason);

        OpenGLStartupSplash splash = new(logo);

        splash.Show();
        splash.Dispose();
        splash.Dispose();

        Assert.Throws<ObjectDisposedException>(splash.Show);
    }

    private sealed class MissingContentSource : IClientContentSource
    {
        public bool TryOpenRead(
            string contentPath,
            ContentLookupMode mode,
            [NotNullWhen(true)] out Stream? stream
        )
        {
            stream = null;
            return false;
        }

        public Stream OpenRequiredRead(string contentPath, ContentLookupMode mode)
        {
            throw new FileNotFoundException($"Content '{contentPath}' is unavailable.");
        }
    }
}
