# Development

OpenConquer Client targets the SDK pinned by [`global.json`](../global.json).

Reference:

- [`architecture/architecture.md`](architecture/architecture.md)
- [`compatibility/native-graphics.md`](compatibility/native-graphics.md)
- [`compatibility/native-text.md`](compatibility/native-text.md)
- [`architecture/launcher-managed-installation.md`](architecture/launcher-managed-installation.md)

## Quality Gate

```bash
dotnet restore OpenConquer.Client.slnx --locked-mode
dotnet format OpenConquer.Client.slnx --verify-no-changes --no-restore
dotnet build OpenConquer.Client.slnx -c Release --no-restore
dotnet test OpenConquer.Client.slnx -c Release --no-build --no-restore
dotnet run --project tools/OpenConquer.Content.Tool -c Release --no-build -- \
  verify-content-set --content-set content/retail-5517
git diff --check
```

Builds use warnings as errors and repository analyzer configuration.

## Run Client

```bash
dotnet run --project src/OpenConquer.Client/OpenConquer.Client.csproj
```

Options:

```text
--window-size WIDTHxHEIGHT
--window-mode resizable|fixed|fullscreen
--presentation fit|integer|stretch
--content-root /path/to/client
```

`ini/GameSetUp.ini` selects the logical render size. Desktop size and presentation are independent.

## Rendering Conformance

Build:

```bash
dotnet build tests/Conformance/OpenConquer.Rendering.Conformance/OpenConquer.Rendering.Conformance.csproj \
  -c Release \
  --no-restore
```

Run against an exact retail 5517 root:

```bash
dotnet run \
  --project tests/Conformance/OpenConquer.Rendering.Conformance/OpenConquer.Rendering.Conformance.csproj \
  -c Release \
  --no-build \
  --no-restore \
  -- \
  --content-root /path/to/retail-5517-root
```

Coverage:

```text
800×600 and 1024×768 logical targets
RGB565 / RGB555 logical color
D16 depth

TGA and DXT3 decoding
production DXT3 decode vs independent reference
alpha and additive blending
RGBA modulation
stretching and source cropping
integer rotation
repeated and degenerate source sampling

native-text placement
coverage
interpolation
batching
atlas synchronization

Progress45 HUD background
Dialog4 HUD panels
Progress40 life
Progress41 mana
Progress46 stamina
Progress47 extended stamina

retail asset hashes
loose/package provenance
independent HUD reference geometry
exact real-driver framebuffer comparison
```

Graphics contracts and driver evidence are maintained in
[`compatibility/native-graphics.md`](compatibility/native-graphics.md).

Text contracts are maintained in
[`compatibility/native-text.md`](compatibility/native-text.md).

## Content

Runtime content:

```text
content/retail-5517/payload
```

Current runtime closure: 19 files.

```text
Data/Main/Logo1.bmp
Data/Main/Logo2.bmp
ani/Control.ani

data/main/ProgressBk.dds

data/main/ProgressForce.dds
data/main/ProgressForce2.dds
data/main/ProgressForce2A.dds
data/main/ProgressForceA.dds

data/main/ProgressHP.dds
data/main/ProgressHPA.dds
data/main/ProgressHPH.dds

data/main/ProgressMP.dds
data/main/ProgressMPA.dds
data/main/ProgressMPH.dds

data/main/mainDialog1.dds
data/main/mainDialog2.dds

ini/GameSetUp.ini
ini/info.ini
ini/package.ini
```

Manifest source casing may differ from logical ANI casing when a loose retail file wins lookup. Current example:

```text
ANI path:    data/main/ProgressForce2A.dds
source path: data/main/ProgressForce2a.dds
```

Compatibility-only assets do not expand the runtime closure.

`Server.dat` remains offline tooling evidence and must not ship with the client.

### Import Retail Content

```bash
dotnet run \
  --project tools/OpenConquer.Content.Tool \
  -c Release \
  -- \
  import-retail-5517 \
  --source /path/to/retail-5517-root \
  --destination /path/to/content-set
```

The destination must not already exist.

### Verify Content Set

Repository set:

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

Published set:

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

Required invariant:

```text
ClientContentClosure
        ==
manifest
        ==
payload
```

### Inspect Server.dat

```bash
dotnet run \
  --project tools/OpenConquer.Content.Tool \
  -- \
  inspect-server-dat \
  --file /path/to/Server.dat
```

## Local Managed Product

Create or refresh:

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

Run:

```bash
./artifacts/local-product/product/OpenConquer.Launcher
```

Windows:

```powershell
.\artifacts\local-product\product\OpenConquer.Launcher.exe
```

Run `create-local-product` when changes affect:

```text
launcher resolution
release integrity
packaged content
installation layout
activation
update
repair
rollback
```

Development signing keys, release sequence state, and coordination locks remain outside repository and product output.

## Test Ownership

```text
OpenConquer.Client.Tests
→ client composition and UI state

OpenConquer.Content.Tests
→ runtime content behavior

OpenConquer.Content.Tool.Tests
→ import, verification, and compatibility tooling

OpenConquer.Launcher.Tests
→ launcher lifecycle and installation

OpenConquer.Platform.Tests
→ desktop/platform behavior

OpenConquer.Product.Tool.Tests
→ product composition

OpenConquer.Rendering.Tests
→ deterministic rendering behavior

OpenConquer.Rendering.Conformance
→ real-driver rendering parity
```

## CI

Linux:

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

Windows/macOS:

```text
locked restore
Release build
tests
```

Real-driver rendering conformance runs separately on supported desktop hardware with exact retail content.

GitHub Actions dependencies must remain pinned to immutable commit SHAs.

## Commit Rule

A slice is complete only after:

```text
implementation
focused tests
documentation
native / real-driver verification when applicable
full quality gate
final re-audit
```
