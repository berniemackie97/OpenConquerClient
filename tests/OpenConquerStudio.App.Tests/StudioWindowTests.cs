using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using OpenConquerStudio.Authoring;
using OpenConquerStudio.Tests;

namespace OpenConquerStudio.App.Tests;

public sealed class StudioWindowTests
{
    private static readonly Lazy<HeadlessUnitTestSession> s_session = new(() => HeadlessUnitTestSession.StartNew(typeof(StudioWindowTests)));

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<StudioApp>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).UseSkia();

    [Fact]
    public Task SelectEditSaveAndReopen_UpdatesPreviewAndPersists() => s_session.Value.Dispatch(async () =>
    {
        using ProjectDirectory directory = new();
        Guid id;
        using (StudioProject project = StudioProject.Create(directory.Project, "Game"))
        {
            id = project.ImportTexture(directory.WriteTga(), "Two colors");
            project.Save();
        }

        StudioWindow window = new();
        window.Show();
        try
        {
            await window.ExecuteAsync(token => window.LoadProject(directory.Project, token));
            ListBox browser = Find<ListBox>(window, "AssetBrowser");
            Assert.Single(browser.Items);
            browser.SelectedIndex = 0;
            await window.PendingOperation;
            Image preview = Find<Image>(window, "TexturePreview");
            Assert.Equal(new PixelSize(2, 1), Assert.IsType<WriteableBitmap>(preview.Source).PixelSize);
            Find<TextBox>(window, "AssetName").Text = "Edited";
            Find<CheckBox>(window, "FlipHorizontal").IsChecked = true;
            Find<ComboBox>(window, "Rotation").SelectedIndex = 1;
            Find<Button>(window, "ApplyAssetEdits").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
            Assert.Equal(new PixelSize(1, 2), Assert.IsType<WriteableBitmap>(preview.Source).PixelSize);
            WriteableBitmap bitmap = Assert.IsType<WriteableBitmap>(preview.Source);
            byte[] displayedPixels = new byte[8];
            using (Avalonia.Platform.ILockedFramebuffer framebuffer = bitmap.Lock())
            {
                Marshal.Copy(framebuffer.Address, displayedPixels, 0, 4);
                Marshal.Copy(framebuffer.Address + framebuffer.RowBytes, displayedPixels, 4, 4);
            }
            Assert.Equal(new byte[] { 0, 255, 0, 255, 255, 0, 0, 255 }, displayedPixels);
            Assert.StartsWith("* ", window.Title);
            Button save = window.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Save"));
            save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
            Assert.DoesNotContain("Unsaved", window.Title);
        }
        finally
        {
            window.Close();
        }

        using StudioProject reopened = StudioProject.Open(directory.Project);
        TextureAsset asset = Assert.Single(reopened.Document.Assets);
        Assert.Equal(id, asset.Id);
        Assert.Equal("Edited", asset.Name);
        Assert.True(asset.FlipHorizontal);
        Assert.Equal(1, asset.ClockwiseQuarterTurns);
        Assert.Equal(new byte[] { 0, 255, 0, 255, 255, 0, 0, 255 }, reopened.Preview(id).Pixels.ToArray());
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task FailedProjectOpen_ShowsDiagnosticAndKeepsCurrentProject() => s_session.Value.Dispatch(async () =>
    {
        using ProjectDirectory directory = new();
        directory.CreateSavedTexture();
        StudioWindow window = new();
        window.Show();
        try
        {
            await window.ExecuteAsync(token => window.LoadProject(directory.Project, token));
            await window.ExecuteAsync(token => window.LoadProject(directory.PathFor("missing"), token));
            Assert.Single(Find<ListBox>(window, "AssetBrowser").Items);
            Assert.NotEmpty(Find<TextBox>(window, "Diagnostics").Text ?? "");
        }
        finally
        {
            window.Close();
        }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task CloseDuringOperation_CancelsAndWaitsBeforeReleasingProject() => s_session.Value.Dispatch(async () =>
    {
        using ProjectDirectory directory = new();
        directory.CreateSavedTexture();
        StudioWindow window = new();
        window.Show();
        await window.ExecuteAsync(token => window.LoadProject(directory.Project, token));
        TaskCompletionSource started = new();
        Task operation = window.ExecuteAsync(async token =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, token);
        });
        await started.Task;
        window.Close();
        await operation;
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { });
        using StudioProject reopened = StudioProject.Open(directory.Project);
        Assert.Equal("Game", reopened.Document.Name);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ArtworkCatalog_ImportsExistingClientImageIntoProject() => s_session.Value.Dispatch(async () =>
    {
        using ProjectDirectory directory = new();
        using (StudioProject project = StudioProject.Create(directory.Project, "Game"))
        {
        }
        StudioWindow window = new();
        window.Show();
        await window.ExecuteAsync(token => window.LoadProject(directory.Project, token));
        Button catalog = window.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Client artwork"));
        catalog.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Window dialog = Assert.Single(window.OwnedWindows);
        ListBox list = Assert.Single(dialog.GetLogicalDescendants().OfType<ListBox>());
        Assert.Equal(8, list.Items.Count);
        list.SelectedIndex = 0;
        Button import = dialog.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Import selected image"));
        import.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await window.PendingOperation;
        Assert.Single(Find<ListBox>(window, "AssetBrowser").Items);
        Assert.Equal(new PixelSize(166, 25), Assert.IsType<WriteableBitmap>(Find<Image>(window, "TexturePreview").Source).PixelSize);
        Button save = window.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Save"));
        save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await window.PendingOperation;
        window.Close();
        using StudioProject reopened = StudioProject.Open(directory.Project);
        Assert.Equal("conquer/login/account/row", Assert.Single(reopened.Document.Assets).SourceKey);
    }, TestContext.Current.CancellationToken);

    private static T Find<T>(Control root, string name) where T : Control => root.GetLogicalDescendants().OfType<T>().Single(c => c.Name == name);
}
