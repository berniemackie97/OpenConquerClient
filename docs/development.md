# Development

OpenConquer Client uses the .NET SDK pinned by [`global.json`](../global.json) and committed NuGet
lock files.

Architecture and ownership rules live in
[`architecture/architecture.md`](architecture/architecture.md). Managed installation, release,
signing, update, repair, and rollback contracts live in
[`architecture/launcher-managed-installation.md`](architecture/launcher-managed-installation.md).

## Build and Verify

```bash
dotnet restore OpenConquer.Client.slnx --locked-mode
dotnet format OpenConquer.Client.slnx --verify-no-changes --no-restore
dotnet build OpenConquer.Client.slnx -c Release --no-restore
dotnet test OpenConquer.Client.slnx -c Release --no-build --no-restore
```

Repository builds use warnings as errors and the analyzers configured by `Directory.Build.props` and
`.editorconfig`.

### OpenGL Conformance

On a supported desktop host, run the production rendering path against the native OpenGL driver and
an exact retail 5517 content root:

```bash
dotnet run \
  --project tests/Conformance/OpenConquer.Rendering.Conformance/OpenConquer.Rendering.Conformance.csproj \
  -c Release \
  --no-build \
  --no-restore \
  -- \
  --content-root /path/to/retail-5517-root
```

The supplied root is the complete retail client root, not an extracted individual asset directory.
Content resolution runs through the production packaged-content boundary, including loose-file and
WDF package lookup according to the retail package configuration.

The conformance executable creates the production OpenGL context and renderer and exercises both
legacy-compatible logical render sizes:

```text
800x600
1024x768
```

It also validates the first verified ANI sprite vertical through the production content and
rendering boundaries:

```text
ani/Common.Ani
└── [Syndicate]
    └── data/pic/Syndicate.tga
```

That case verifies:

- ANI section and frame resolution
- exact encoded retail TGA identity
- exact independently established decoded RGBA identity
- production OpenGL texture upload
- default retail-compatible sprite blending and rasterization
- rendering into the production 16-bit logical target
- exact RGB565 or RGB555 framebuffer output

The current verified framebuffer oracles are documented in
[`compatibility/native-graphics.md`](compatibility/native-graphics.md).

The project is built by Windows and macOS CI as part of the solution. Execute the conformance
boundary on a supported desktop host with a usable native OpenGL driver and access to the exact
retail 5517 content tree.

## Game Client

Run the client directly:

```bash
dotnet run --project src/OpenConquer.Client/OpenConquer.Client.csproj
```

Current desktop options:

```text
--window-size WIDTHxHEIGHT
--window-mode resizable|fixed|fullscreen
--presentation fit|integer|stretch
--content-root /path/to/client
```

`ini/GameSetUp.ini` selects the legacy-compatible logical render surface. Physical desktop size,
window mode, and presentation remain separate host concerns.

The packaged runtime content currently resolves beneath:

```text
content/retail-5517/payload
```

Current runtime closure:

```text
data/main/Logo1.bmp
data/main/Logo2.bmp
ini/GameSetUp.ini
ini/info.ini
ini/package.ini
```

The ANI/TGA Syndicate asset used by rendering conformance is retail compatibility evidence and does
not expand the managed runtime content closure by itself.

Retail `Server.dat` is offline compatibility evidence only and must not ship with the runtime
client.

Inspect one explicitly with:

```bash
dotnet run \
  --project tools/OpenConquer.Content.Tool \
  -- \
  inspect-server-dat \
  --file /path/to/Server.dat
```

## Local Managed Product

The launcher and client publish independently and are composed into an authenticated managed
product.

Create or refresh the canonical local product:

```bash
dotnet run \
  --project tools/OpenConquer.Product.Tool \
  -c Release \
  -- \
  create-local-product
```

The canonical repository-local layout is:

```text
artifacts/local-product/
├── work/
├── product/
└── product.previous/
```

`product/` is active. `product.previous/` is the retained rollback generation. `work/` contains
isolated per-run composition state and is normally empty after successful cleanup.

Run the managed launcher:

macOS/Linux:

```bash
./artifacts/local-product/product/OpenConquer.Launcher
```

Windows:

```powershell
.\artifacts\local-product\product\OpenConquer.Launcher.exe
```

A healthy authenticated local installation reports:

```text
OpenConquer is ready
Release local-N is verified for this device.
```

Development publisher keys, release-sequence state, and coordination locks live outside the
repository in the current user's platform configuration directory. They must never enter source
control or managed-product output.

The Product Tool owns development orchestration only. Production private signing keys remain outside
the Product Tool.

For release metadata, package, catalog, signing, activation, repair, rollback, and trust contracts,
use [`architecture/launcher-managed-installation.md`](architecture/launcher-managed-installation.md)
instead of duplicating those contracts here.

## Content Verification

Verify the tracked retail content set:

```bash
dotnet run \
  --project tools/OpenConquer.Content.Tool \
  -c Release \
  --no-build \
  --no-restore \
  -- \
  verify-content-set \
  --content-set content/retail-5517
```

Verify a published client by pointing the same command at its packaged content set:

```bash
dotnet run \
  --project tools/OpenConquer.Content.Tool \
  -c Release \
  --no-build \
  --no-restore \
  -- \
  verify-content-set \
  --content-set /path/to/client-publish/content/retail-5517
```

Tracked content, the runtime consumer closure, and published client content must remain consistent.

## Tests

```text
tests/
├── Conformance/
│   └── OpenConquer.Rendering.Conformance/
├── OpenConquer.Client.Tests/
├── OpenConquer.Content.Tests/
├── OpenConquer.Content.Tool.Tests/
├── OpenConquer.Launcher.Tests/
├── OpenConquer.Platform.Tests/
├── OpenConquer.Product.Tool.Tests/
└── OpenConquer.Rendering.Tests/
```

Ownership:

- Client tests: startup and game-runtime composition.
- Content tests: runtime content and legacy formats.
- Launcher tests: launcher lifecycle, settings, release verification, and product boundaries.
- Product Tool tests: release metadata, local-product orchestration, activation, and composition.
- Platform tests: desktop host mechanics.
- Rendering tests: graphics behavior that does not require a live native driver.
- Rendering conformance: production OpenGL context, render-target, presentation, retail ANI/TGA
  decoding, sprite rendering, and exact framebuffer behavior on a real driver.

## Continuous Integration

CI runs:

```text
Linux
├── locked restore
├── formatting
├── Release build
├── complete test suite
├── launcher isolation
├── content verification
├── client and launcher publish verification
└── authenticated release/product composition

Windows
├── locked restore
├── Release build
└── complete test suite

macOS
├── locked restore
├── Release build
└── complete test suite
```

The conformance project is restored and built on Windows and macOS through the solution but requires
a supported desktop host with a usable native OpenGL driver and exact retail evidence for execution.

GitHub Actions dependencies are pinned to immutable commit SHAs.

## Before Committing

Run the local quality gate:

```bash
dotnet restore OpenConquer.Client.slnx --locked-mode
dotnet format OpenConquer.Client.slnx --verify-no-changes --no-restore
dotnet build OpenConquer.Client.slnx -c Release --no-restore
dotnet test OpenConquer.Client.slnx -c Release --no-build --no-restore
git diff --check
```

For rendering changes on a supported desktop host:

```bash
dotnet run \
  --project tests/Conformance/OpenConquer.Rendering.Conformance/OpenConquer.Rendering.Conformance.csproj \
  -c Release \
  --no-build \
  --no-restore \
  -- \
  --content-root /path/to/retail-5517-root
```

Run `create-local-product` when the slice affects launcher resolution, release integrity, content
publication, installation layout, activation, update, repair, or rollback.

Commit only after implementation, tests, relevant documentation, applicable native verification, and
the final slice audit are clean.
