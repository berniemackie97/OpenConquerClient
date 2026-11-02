# OpenConquerStudio

OpenConquerStudio is one game-development platform: runtime content, authoring, importers,
the desktop workspace, and automation tools belong to the same product. OpenConquerClient
is its first game integration. The foundation currently authors textures; it is not a
complete scene editor or a replacement for the client's content pipeline yet.

## Build and run

Use the .NET SDK pinned in the repository's `global.json`.

```sh
dotnet restore OpenConquerStudio.slnx --locked-mode
dotnet format OpenConquerStudio.slnx --verify-no-changes --no-restore
dotnet build OpenConquerStudio.slnx --configuration Release --no-restore
dotnet test OpenConquerStudio.slnx --configuration Release --no-build --no-restore
python3 -m unittest discover -s tests/development -p test_studio_cli.py
dotnet run --project src/OpenConquerStudio --configuration Release --no-build
```

An optional project-directory argument opens a project at startup:

```sh
dotnet run --project src/OpenConquerStudio --configuration Release --no-build -- /path/to/project
```

The Studio workflow builds and tests on Linux, Windows, and macOS. Headless desktop tests
exercise real controls and bitmap pixels; they do not certify native file pickers,
accessibility, display scaling, or a game-rendering viewport on every desktop.

## Desktop workflow

1. Select **New project** and choose an existing empty directory. Rename the project using
   the project-name field and **Rename project**.
2. Use **Import texture** for a supported TGA/DDS file, **Client artwork** for a supported
   existing client image, or **New texture** to create an image without source assets.
3. Select the asset. Its pixels, dimensions, identity, and source type appear in the workspace.
4. Change its name, horizontal flip, or clockwise rotation, then select **Apply asset edits**.
   Changes affect both the preview and subsequent compilation. **Replace source image** imports a new TGA/DDS into the selected asset while preserving its GUID, name, original source association, and edit settings. New textures accept dimensions
   and an RGB fill color; this slice is not a painting application.
5. **Save**, close, and reopen the project. Imported files and embedded source artwork remain
   unchanged. Closing or switching projects offers Save, Discard, and Cancel for applied edits.
6. **Save and build** creates a new content directory beneath the selected parent, validates
   every compiled image, and reports the directory. Existing build directories are never replaced.

Fields are committed by their Apply/Rename buttons; text still being typed is not a project edit.
Imports, decoding, preview transforms, saves, and builds run off the UI thread. Only one operation
runs per window. **Cancel operation** cancels at operation boundaries; an individual image decoder
or atomic file publication finishes its current bounded operation first. Closing waits for active
work before releasing resources.

TGA import uses the existing 32-bit RLE decoder: no color map or image ID, zero origin,
bottom-left orientation, and descriptor `0x08`. DDS supports the existing DXT1/DXT3 subset;
mipmaps, volume textures, and other compression formats are rejected. Unsupported inputs
produce diagnostics instead of partial assets.

The client-artwork catalog initially covers eight account rows, dialog images, and button
states. These are individual image imports. Dialog placement, timelines, masks, and state
machines remain client behavior and are not converted into editable UI scenes here.

## Automation

The command-line host has the same authoring and compilation implementation as the desktop.
Each invocation releases its project lease before exiting. Run it against a project that is
closed in Studio.

```sh
mkdir my-game
dotnet run --project tools/OpenConquerStudio.Tool -c Release --no-build -- create my-game "My game"
dotnet run --project tools/OpenConquerStudio.Tool -c Release --no-build -- import my-game image.tga "Sprite"
dotnet run --project tools/OpenConquerStudio.Tool -c Release --no-build -- list-artwork
dotnet run --project tools/OpenConquerStudio.Tool -c Release --no-build -- import-artwork my-game conquer/login/account/row
dotnet run --project tools/OpenConquerStudio.Tool -c Release --no-build -- new-texture my-game "Blue" 64 64 0 0 255
dotnet run --project tools/OpenConquerStudio.Tool -c Release --no-build -- edit my-game ASSET-GUID "Renamed" true 1
dotnet run --project tools/OpenConquerStudio.Tool -c Release --no-build -- build my-game compiled-content
dotnet run --project tools/OpenConquerStudio.Tool -c Release --no-build -- verify compiled-content
```

Import/create commands print the new asset GUID. `edit` takes horizontal flip and clockwise
quarter turns, from 0 to 3. Exit codes are 0 for success, 1 for a rejected operation, 2 for
usage errors, and 130 for cancellation. `build` requires a saved project and a nonexistent
destination whose parent exists. Builds inside a project must be beneath `Builds/`.

## Boundaries and next work

Runtime texture loading requires neither Studio UI nor Conquer content. This Studio build also
includes the existing content assembly as the source for its explicit client-artwork catalog;
creating a new game does not require importing those sources. No extra artwork copies or source
archives have been added to the repository.

The game does not yet load `.octex` assets. A later coordinated slice must introduce explicit
game resource references, preserve required UI layout/state semantics, and bind the resulting
assets into the existing authenticated release and installation contracts. A source key records
where an image came from; it does not automatically redirect a client resource.

Map/scene editing, animation authoring, sprite-sheet slicing, materials, shaders, terrain,
entities, UI layouts, audio, package signing, and publishing are deferred. There are no controls
or placeholder implementations for them. The next useful slice is a sprite scene with stable
texture references, dependency validation, and a verified adapter for the shared game renderer.

See [architecture](architecture.md), [formats and safety](formats.md), and
[repository audit](audit.md) for ownership and integration details.
