using System.Globalization;
using System.Text.Json;
using OpenConquerStudio.Authoring;
using OpenConquerStudio.ConquerImport;

using CancellationTokenSource cancellation = new();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
try
{
    switch (args)
    {
        case ["create", string root, string name]:
            using (StudioProject created = StudioProject.Create(root, name))
            {
                Console.WriteLine(created.Document.Id);
            }

            break;
        case ["list-artwork"]:
            foreach (ArtworkSource source in ConquerArtworkImporter.Sources)
            {
                Console.WriteLine($"{source.Key}  {source.Name}");
            }

            break;
        case ["import-artwork", string root, string key]:
            using (StudioProject project = StudioProject.Open(root, cancellation.Token))
            {
                Guid id = ConquerArtworkImporter.Import(project, key, cancellation.Token);
                project.Save(cancellation.Token);
                Console.WriteLine(id);
            }

            break;
        case ["import", string root, string source, string name]:
            using (StudioProject project = StudioProject.Open(root, cancellation.Token))
            {
                Guid id = project.ImportTexture(source, name, cancellation.Token);
                project.Save(cancellation.Token);
                Console.WriteLine(id);
            }

            break;
        case ["new-texture", string root, string name, string width, string height, string red, string green, string blue]:
            using (StudioProject project = StudioProject.Open(root, cancellation.Token))
            {
                Guid id = project.CreateTexture(name, int.Parse(width, CultureInfo.InvariantCulture), int.Parse(height, CultureInfo.InvariantCulture),
                    byte.Parse(red, CultureInfo.InvariantCulture), byte.Parse(green, CultureInfo.InvariantCulture), byte.Parse(blue, CultureInfo.InvariantCulture));
                project.Save(cancellation.Token);
                Console.WriteLine(id);
            }

            break;
        case ["edit", string root, string id, string name, string flip, string turns]:
            using (StudioProject project = StudioProject.Open(root, cancellation.Token))
            {
                project.EditTexture(Guid.Parse(id), name, bool.Parse(flip), int.Parse(turns, CultureInfo.InvariantCulture));
                project.Save(cancellation.Token);
            }

            break;
        case ["replace", string root, string id, string source]:
            using (StudioProject project = StudioProject.Open(root, cancellation.Token))
            {
                project.ReplaceTexture(Guid.Parse(id), source, cancellation.Token);
                project.Save(cancellation.Token);
            }

            break;
        case ["build", string root, string destination]:
            using (StudioProject project = StudioProject.Open(root, cancellation.Token))
            {
                ContentBuild build = StudioCompiler.Compile(project, destination, cancellation.Token);
                Console.WriteLine($"Compiled and validated {build.Textures.Length} textures.");
            }

            break;
        case ["verify", string directory]:
            ContentBuild verified = StudioCompiler.Validate(directory, cancellation.Token);
            Console.WriteLine($"Validated {verified.Textures.Length} textures for project {verified.ProjectId}.");
            break;
        default:
            Console.Error.WriteLine("Usage:\n  create <empty-directory> <name>\n  list-artwork\n  import-artwork <project-directory> <artwork-key>\n  import <project-directory> <source.tga|source.dds> <name>\n  new-texture <project-directory> <name> <width> <height> <red> <green> <blue>\n  edit <project-directory> <asset-id> <name> <flip-horizontal:true|false> <clockwise-quarter-turns:0..3>\n  replace <project-directory> <asset-id> <source.tga|source.dds>\n  build <project-directory> <new-output-directory>\n  verify <output-directory>");
            return 2;
    }

    return 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Operation canceled.");
    return 130;
}
catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or FormatException or OverflowException or InvalidOperationException)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
