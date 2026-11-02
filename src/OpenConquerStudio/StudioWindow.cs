using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using OpenConquerStudio.Authoring;
using OpenConquerStudio.ConquerImport;
using OpenConquerStudio.Runtime;

namespace OpenConquerStudio;

public sealed class StudioWindow : Window
{
    private readonly StackPanel _commands = new() { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(12) };
    private readonly ListBox _assets = new() { Name = "AssetBrowser" };
    private readonly TextBox _projectName = new() { PlaceholderText = "Project name", Name = "ProjectName" };
    private readonly TextBox _assetName = new() { PlaceholderText = "Asset name", Name = "AssetName" };
    private readonly CheckBox _flip = new() { Content = "Flip horizontally", Name = "FlipHorizontal" };
    private readonly ComboBox _rotation = new() { ItemsSource = new[] { "0°", "90° clockwise", "180°", "270° clockwise" }, SelectedIndex = 0, Name = "Rotation" };
    private readonly TextBlock _metadata = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBox _output = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 90, Name = "Diagnostics" };
    private readonly Image _preview = new() { Stretch = Stretch.Uniform, Margin = new Thickness(24), Name = "TexturePreview" };
    private readonly TextBlock _projectStatus = new() { Margin = new Thickness(12, 0), TextWrapping = TextWrapping.Wrap };
    private readonly List<Control> _projectCommands = [];
    private readonly List<Control> _assetCommands = [];
    private readonly Button _cancel = new() { Content = "Cancel operation", IsEnabled = false };
    private StudioProject? _project;
    private WriteableBitmap? _bitmap;
    private CancellationTokenSource? _cancellation;
    private Task? _operation;
    private bool _busy;
    private bool _closing;
    private bool _refreshing;
    private bool _allowClose;

    public StudioWindow(string? projectRoot = null)
    {
        Title = "OpenConquerStudio";
        Width = 1280;
        Height = 760;
        MinWidth = 1150;
        MinHeight = 600;
        AddCommand("New project", NewProject);
        AddCommand("Open project", OpenProject);
        AddCommand("Import texture", ImportTexture, requiresProject: true);
        AddCommand("Client artwork", ImportArtwork, requiresProject: true);
        AddCommand("New texture", CreateTexture, requiresProject: true);
        AddCommand("Save", SaveProject, requiresProject: true);
        AddCommand("Save and build", Build, requiresProject: true);
        _cancel.Click += (_, _) => _cancellation?.Cancel();
        _commands.Children.Add(_cancel);

        Button rename = new()
        {
            Content = "Rename project"
        };
        rename.Click += async (_, _) => await ExecuteAsync(_ => { _project!.Rename(_projectName.Text ?? ""); return Task.CompletedTask; });
        _projectCommands.Add(rename);
        Button apply = new()
        {
            Content = "Apply asset edits",
            Name = "ApplyAssetEdits"
        };
        apply.Click += async (_, _) => await ExecuteAsync(async token =>
        {
            if (_assets.SelectedItem is AssetRow row)
            {
                _project!.EditTexture(row.Id, _assetName.Text ?? "", _flip.IsChecked == true, _rotation.SelectedIndex);
                await UpdatePreview(row.Id, token);
            }
        });
        _assetCommands.Add(apply);
        Button replace = new()
        {
            Content = "Replace source image"
        };
        replace.Click += async (_, _) => await ExecuteAsync(async token =>
        {
            if (_assets.SelectedItem is AssetRow row)
            {
                string? path = await StudioDialogs.PickTexture(this);
                if (path is not null)
                {
                    await Task.Run(() => _project!.ReplaceTexture(row.Id, path, token), token);
                    await UpdatePreview(row.Id, token);
                    _output.Text = "Source image replaced. Asset identity and edit settings were preserved.";
                }
            }
        });
        _assetCommands.Add(replace);
        _assetCommands.Add(_assetName);
        _assetCommands.Add(_flip);
        _assetCommands.Add(_rotation);
        _projectCommands.Add(_projectName);
        _assets.SelectionChanged += async (_, _) =>
        {
            if (!_busy && !_refreshing && _assets.SelectedItem is AssetRow row)
            {
                await ExecuteAsync(token => UpdatePreview(row.Id, token));
            }
        };

        Grid body = new()
        {
            ColumnDefinitions = new ColumnDefinitions("240,5,*,5,275"),
            Margin = new Thickness(12)
        };
        Grid browser = new()
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*"),
            RowSpacing = 8
        };
        Control[] browserControls = [Heading("Project"), _projectName, rename, Heading("Assets"), _assets];
        for (int row = 0; row < browserControls.Length; row++)
        {
            Grid.SetRow(browserControls[row], row);
            browser.Children.Add(browserControls[row]);
        }
        Grid.SetColumn(browser, 0);
        body.Children.Add(browser);
        body.Children.Add(new GridSplitter { Width = 5, HorizontalAlignment = HorizontalAlignment.Stretch, [Grid.ColumnProperty] = 1 });
        Border previewBorder = new()
        {
            Background = new SolidColorBrush(Color.FromRgb(38, 42, 48)),
            Child = _preview,
            [Grid.ColumnProperty] = 2
        };
        body.Children.Add(previewBorder);
        body.Children.Add(new GridSplitter { Width = 5, HorizontalAlignment = HorizontalAlignment.Stretch, [Grid.ColumnProperty] = 3 });
        StackPanel inspector = new()
        {
            Spacing = 10,
            Margin = new Thickness(12, 0, 0, 0),
            [Grid.ColumnProperty] = 4,
            Children = { Heading("Texture"), _metadata, _assetName, _flip, _rotation, apply, replace }
        };
        body.Children.Add(new ScrollViewer { Content = inspector, [Grid.ColumnProperty] = 4, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        Grid root = new()
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*,120")
        };
        root.Children.Add(_commands);
        Grid.SetRow(_projectStatus, 1);
        root.Children.Add(_projectStatus);
        Grid.SetRow(body, 2);
        root.Children.Add(body);
        Border output = new()
        {
            Margin = new Thickness(12, 0, 12, 12),
            Child = _output,
            [Grid.RowProperty] = 3
        };
        root.Children.Add(output);
        Content = root;
        _preview.SetValue(AutomationProperties.NameProperty, "Authored texture preview");
        _projectName.SetValue(AutomationProperties.NameProperty, "Project name");
        _assetName.SetValue(AutomationProperties.NameProperty, "Asset name");
        _rotation.SetValue(AutomationProperties.NameProperty, "Clockwise rotation");
        if (projectRoot is not null)
        {
            Opened += async (_, _) => await ExecuteAsync(token => LoadProject(projectRoot, token));
        }
        Closing += HandleClosing;
        Closed += (_, _) => { _project?.Dispose(); _bitmap?.Dispose(); };
        Refresh();
        _output.Text = "Create an empty project, then import a TGA/DDS texture or create a new texture.";
    }

    private static TextBlock Heading(string text) => new() { Text = text, FontWeight = FontWeight.SemiBold, FontSize = 16 };

    private void AddCommand(string text, Func<CancellationToken, Task> action, bool requiresProject = false)
    {
        Button button = new()
        {
            Content = text
        };
        button.Click += async (_, _) => await ExecuteAsync(action);
        _commands.Children.Add(button);
        if (requiresProject)
        {
            _projectCommands.Add(button);
        }
    }

    internal Task PendingOperation => _operation ?? Task.CompletedTask;

    internal Task ExecuteAsync(Func<CancellationToken, Task> action)
    {
        if (_busy)
        {
            return Task.CompletedTask;
        }

        _operation = ExecuteCore(action);
        return _operation;
    }

    private async Task ExecuteCore(Func<CancellationToken, Task> action)
    {
        _busy = true;
        using CancellationTokenSource cancellation = new();
        _cancellation = cancellation;
        RefreshEnabled();
        try
        {
            await action(cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            _output.Text = "Operation canceled. Any edits remain available to save.";
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException)
        {
            _output.Text = exception.Message;
        }
        finally
        {
            _cancellation = null;
            _busy = false;
            Refresh();
        }
    }

    private async Task<bool> CanReplaceProject(CancellationToken token)
    {
        if (_project?.IsModified != true)
        {
            return true;
        }

        string choice = await StudioDialogs.ConfirmChanges(this);
        if (choice == "Save")
        {
            await Task.Run(() => _project.Save(token), token);
        }

        return choice != "Cancel";
    }

    private async Task NewProject(CancellationToken token)
    {
        if (!await CanReplaceProject(token))
        {
            return;
        }

        string? root = await StudioDialogs.PickFolder(this, "Select an empty directory for the project");
        if (root is not null)
        {
            StudioProject candidate = await Task.Run(() => StudioProject.Create(root, Path.GetFileName(root)), token);
            ReplaceProject(candidate);
        }
    }

    private async Task OpenProject(CancellationToken token)
    {
        if (!await CanReplaceProject(token))
        {
            return;
        }

        string? root = await StudioDialogs.PickFolder(this, "Open project directory");
        if (root is not null && root != _project?.Root)
        {
            await LoadProject(root, token);
        }
    }

    internal async Task LoadProject(string root, CancellationToken token)
    {
        StudioProject candidate = await Task.Run(() => StudioProject.Open(root, token), token);
        ReplaceProject(candidate);
    }

    private void ReplaceProject(StudioProject candidate)
    {
        _project?.Dispose();
        _project = candidate;
        _preview.Source = null;
        _bitmap?.Dispose();
        _bitmap = null;
        _metadata.Text = "Select a texture to inspect its properties.";
        _output.Text = "Project opened: " + candidate.Root;
    }

    private async Task ImportTexture(CancellationToken token)
    {
        string? path = await StudioDialogs.PickTexture(this);
        if (path is not null)
        {
            Guid id = await Task.Run(() => _project!.ImportTexture(path, Path.GetFileNameWithoutExtension(path), token), token);
            await SelectAsset(id, token);
            _output.Text = "Texture imported. Save the project to persist it.";
        }
    }

    private async Task ImportArtwork(CancellationToken token)
    {
        string? key = await StudioDialogs.PickArtwork(this);
        if (key is not null)
        {
            Guid id = await Task.Run(() => ConquerArtworkImporter.Import(_project!, key, token), token);
            await SelectAsset(id, token);
            _output.Text = "Client artwork imported. Save the project to persist your editable copy.";
        }
    }

    private async Task CreateTexture(CancellationToken token)
    {
        TextureCreation? creation = await StudioDialogs.CreateTexture(this);
        if (creation is not null)
        {
            Guid id = await Task.Run(() => _project!.CreateTexture(creation.Name, creation.Width, creation.Height, creation.Red, creation.Green, creation.Blue), token);
            await SelectAsset(id, token);
            _output.Text = "Texture created. Save the project to persist it.";
        }
    }

    private async Task SelectAsset(Guid id, CancellationToken token)
    {
        RefreshAssets(id);
        await UpdatePreview(id, token);
    }

    private async Task UpdatePreview(Guid id, CancellationToken token)
    {
        TextureAsset asset = _project!.Document.Assets.Single(a => a.Id == id);
        TextureData texture = await Task.Run(() => _project.Preview(id, token), token);
        WriteableBitmap bitmap = new(new PixelSize(texture.Width, texture.Height), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
        try
        {
            byte[] pixels = texture.Pixels.ToArray();
            using ILockedFramebuffer framebuffer = bitmap.Lock();
            for (int y = 0; y < texture.Height; y++)
            {
                Marshal.Copy(pixels, y * texture.Width * 4, framebuffer.Address + y * framebuffer.RowBytes, texture.Width * 4);
            }
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }

        _preview.Source = bitmap;
        _bitmap?.Dispose();
        _bitmap = bitmap;
        _assetName.Text = asset.Name;
        _flip.IsChecked = asset.FlipHorizontal;
        _rotation.SelectedIndex = asset.ClockwiseQuarterTurns;
        _metadata.Text = $"{texture.Width} × {texture.Height} RGBA8\nID: {asset.Id}\nSource: {asset.SourceFormat}";
    }

    private async Task SaveProject(CancellationToken token)
    {
        await Task.Run(() => _project!.Save(token), token);
        _output.Text = "Project saved.";
    }

    private async Task Build(CancellationToken token)
    {
        string? parent = await StudioDialogs.PickFolder(this, "Select a directory to hold the new build");
        if (parent is null)
        {
            return;
        }

        string destination = Path.Combine(parent, "content-" + Guid.NewGuid().ToString("N"));
        ContentBuild build = await Task.Run(() => { _project!.Save(token); return StudioCompiler.Compile(_project, destination, token); }, token);
        _output.Text = $"Compiled and validated {build.Textures.Length} textures.\n{destination}";
    }

    private void Refresh()
    {
        _projectName.Text = _project?.Document.Name ?? "";
        _projectStatus.Text = _project is null ? "No project open" : $"{_project.Root}  |  {_project.Document.Id}  {(_project.IsModified ? "• Unsaved changes" : "• Saved")}";
        Title = (_project?.IsModified == true ? "* " : "") + "OpenConquerStudio" + (_project is null ? "" : " — " + _project.Document.Name);
        RefreshAssets((_assets.SelectedItem as AssetRow)?.Id);
        RefreshEnabled();
    }

    private void RefreshAssets(Guid? selected)
    {
        AssetRow[] rows = _project?.Document.Assets.Select(a => new AssetRow(a.Id, a.Name)).ToArray() ?? [];
        _refreshing = true;
        try
        {
            _assets.ItemsSource = rows;
            _assets.SelectedItem = rows.FirstOrDefault(a => a.Id == selected);
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void RefreshEnabled()
    {
        foreach (Control command in _commands.Children)
        {
            command.IsEnabled = !_busy;
        }

        foreach (Control command in _projectCommands)
        {
            command.IsEnabled = !_busy && _project is not null;
        }

        foreach (Control command in _assetCommands)
        {
            command.IsEnabled = !_busy && _assets.SelectedItem is AssetRow;
        }

        _assets.IsEnabled = !_busy;
        _cancel.IsEnabled = _busy;
    }

    private async void HandleClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        if (_closing)
        {
            return;
        }

        _closing = true;
        try
        {
            _cancellation?.Cancel();
            if (_operation is not null)
            {
                await _operation;
            }

            bool close = false;
            await ExecuteAsync(async token => close = await CanReplaceProject(token));
            if (close)
            {
                _allowClose = true;
                Close();
            }
        }
        finally
        {
            _closing = false;
        }
    }

    private sealed record AssetRow(Guid Id, string Name)
    {
        public override string ToString() => Name;
    }
}
