# Development

OpenConquer Client targets the SDK pinned by [`global.json`](../global.json).

Architecture rules: [`architecture/architecture.md`](architecture/architecture.md) Native graphics
contracts: [`compatibility/native-graphics.md`](compatibility/native-graphics.md) Native text
contracts: [`compatibility/native-text.md`](compatibility/native-text.md) Launcher/install
contracts:
[`architecture/launcher-managed-installation.md`](architecture/launcher-managed-installation.md)

## Quality Gate

Before committing:

```bash
dotnet restore OpenConquer.Client.slnx --locked-mode
dotnet format OpenConquer.Client.slnx --verify-no-changes --no-restore
dotnet build OpenConquer.Client.slnx -c Release --no-restore
dotnet test OpenConquer.Client.slnx -c Release --no-build --no-restore
git diff --check
```

Repository builds use warnings as errors and the analyzer configuration in `Directory.Build.props`
and `.editorconfig`.

## Run Client

```bash
dotnet run --project src/OpenConquer.Client/OpenConquer.Client.csproj
```

Desktop options:

```text
--window-size WIDTHxHEIGHT
--window-mode resizable|fixed|fullscreen
--presentation fit|integer|stretch
--content-root /path/to/client
```

`ini/GameSetUp.ini` selects the retail-compatible logical render size. Physical window size, window
mode, and presentation are separate desktop-host concerns.

## Rendering Conformance

Build first:

```bash
dotnet build tests/Conformance/OpenConquer.Rendering.Conformance/OpenConquer.Rendering.Conformance.csproj -c Release --no-restore
```

Run against an exact retail 5517 client root:

```bash
dotnet run \
  --project tests/Conformance/OpenConquer.Rendering.Conformance/OpenConquer.Rendering.Conformance.csproj \
  -c Release \
  --no-build \
  --no-restore \
  -- \
  --content-root /path/to/retail-5517-root
```

The supplied path must be the complete retail root. Content resolution uses the production
loose-file/WDF lookup path.

Current conformance covers:

- 800×600 and 1024×768 logical targets;
- production OpenGL context and renderer;
- retail-compatible RGB565/RGB555 logical color precision;
- exact D16 depth allocation;
- ANI lookup through `ani/Common.Ani` and `ani/weather.ani`;
- verified `data/pic/Syndicate.tga` identity and TGA decoding;
- verified package-backed `data/firework/yinfa1/1.dds` identity and DXT3 decoding;
- byte-exact production DXT3 output against an independent reference decoder;
- default alpha blending and explicit RGBA sprite modulation;
- exact synthetic `ONE / ONE` additive-blend behavior;
- verified retail DXT3 firework additive rendering with visible RGB contribution and output distinct
  from alpha blending;
- whole-texture natural-size drawing;
- whole-texture stretching;
- source-region stretching;
- integer-degree sprite rotation;
- exact framebuffer comparison on a real driver.

Expected graphics contracts, fixture identities, and verified framebuffer hashes are documented in
[`compatibility/native-graphics.md`](compatibility/native-graphics.md).

Native text input contracts are documented in
[`compatibility/native-text.md`](compatibility/native-text.md) and are currently verified through
driver-independent Content and Rendering unit tests. Pixel-level text conformance remains deferred
until the rasterizer and text-rendering path are implemented.

## Content

Runtime retail content is resolved beneath:

```text
content/retail-5517/payload
```

Current managed runtime closure:

```text
data/main/Logo1.bmp
data/main/Logo2.bmp
ini/GameSetUp.ini
ini/info.ini
ini/package.ini
```

The Syndicate ANI/TGA and weather/firework ANI/DXT3 assets used by rendering conformance are
compatibility evidence and do not expand the packaged runtime closure by themselves. Production
format support and checked-in runtime asset dependencies remain separate concerns.

Retail `Server.dat` is offline compatibility evidence and must not ship with the client.

Inspect one with:

```bash
dotnet run \
  --project tools/OpenConquer.Content.Tool \
  -- \
  inspect-server-dat \
  --file /path/to/Server.dat
```

### Verify Content Set

Repository content:

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

Published content:

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

Tracked content, runtime closure, and published content must remain consistent.

## Local Managed Product

Create or refresh the local managed product:

```bash
dotnet run \
  --project tools/OpenConquer.Product.Tool \
  -c Release \
  -- \
  create-local-product
```

Layout:

```text
artifacts/local-product/
├── work/
├── product/
└── product.previous/
```

- `product/` — active installation
- `product.previous/` — rollback generation
- `work/` — transient composition state

Run on macOS/Linux:

```bash
./artifacts/local-product/product/OpenConquer.Launcher
```

Run on Windows:

```powershell
.\artifacts\local-product\product\OpenConquer.Launcher.exe
```

A healthy installation reports:

```text
OpenConquer is ready
Release local-N is verified for this device.
```

Development signing keys, release-sequence state, and coordination locks must remain outside the
repository and managed-product output.

Run `create-local-product` when changes affect:

- launcher resolution;
- release integrity;
- packaged content;
- installation layout;
- activation;
- update;
- repair;
- rollback.

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

- **Client** — startup and runtime composition.
- **Content** — runtime content and legacy formats.
- **Content Tool** — content inspection and verification.
- **Launcher** — launcher lifecycle, settings, releases, and product boundaries.
- **Platform** — desktop host mechanics.
- **Product Tool** — local product composition and activation.
- **Rendering** — graphics and rendering-facing text behavior not requiring a native driver.
- **Rendering Conformance** — real-driver rendering and exact framebuffer behavior.

## CI

### Linux

```text
locked restore
format verification
Release build
tests
launcher isolation
content verification
publish verification
authenticated product composition
```

### Windows

```text
locked restore
Release build
tests
```

### macOS

```text
locked restore
Release build
tests
```

The rendering conformance project builds in CI but requires a desktop OpenGL driver and exact retail
content for execution.

GitHub Actions dependencies must remain pinned to immutable commit SHAs.

## Commit Rule

Commit only when the slice has:

1. completed implementation;
2. completed relevant tests;
3. updated relevant documentation;
4. passed applicable native/real-driver verification;
5. passed the full quality gate;
6. passed final slice re-audit.
