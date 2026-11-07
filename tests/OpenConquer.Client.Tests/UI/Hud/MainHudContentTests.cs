using System.Diagnostics.CodeAnalysis;
using OpenConquer.Client.UI.Hud.Rendering;
using OpenConquer.Content;
using OpenConquer.Content.Configuration;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudContentTests
{
    [Fact]
    public void CancellationDuringSettingsReadStopsBeforeReadingFontsOrArtwork()
    {
        using CancellationTokenSource stop = new();
        SettingsSource source = new(stop);

        Assert.Throws<OperationCanceledException>(() => MainHudContent.Load(source, stop.Token));

        Assert.True(source.ReadSettings);
        Assert.DoesNotContain(GameFontConfiguration.RelativePath, source.Attempts, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("ani/Control.ani", source.Attempts, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void RequiredStartupContentStillReportsAFileFailure()
    {
        using TemporaryContentDirectory directory = new();
        ClientContentRoot source = new(directory.RootPath);

        FileNotFoundException failure = Assert.Throws<FileNotFoundException>(
            () => MainHudContent.Load(source, TestContext.Current.CancellationToken));

        Assert.Contains(SelectedMagicCooldownTextConfiguration.RelativePath, failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class SettingsSource(CancellationTokenSource stop) : IClientContentSource
    {
        public List<string> Attempts { get; } = [];

        public bool ReadSettings
        {
            get; private set;
        }

        public bool TryOpenRead(string path, ContentLookupMode mode, [NotNullWhen(true)] out Stream? stream)
        {
            Attempts.Add(path);
            stream = null;
            if (path != SelectedMagicCooldownTextConfiguration.RelativePath)
            {
                return false;
            }

            if (!ReadSettings)
            {
                ReadSettings = true;
                stop.Cancel();
            }

            stream = new MemoryStream("[SelectMagicNum]\nFontSize=20\n"u8.ToArray());
            return true;
        }

        public Stream OpenRequiredRead(string path, ContentLookupMode mode) =>
            TryOpenRead(path, mode, out Stream? stream) ? stream : throw new FileNotFoundException(path);
    }
}
