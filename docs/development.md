# Development

OpenConquer Client targets the SDK pinned by [`global.json`](../global.json).

Reference:

- [`architecture/architecture.md`](architecture/architecture.md)
- [`compatibility/native-graphics.md`](compatibility/native-graphics.md)
- [`compatibility/native-text.md`](compatibility/native-text.md)
- [`content/retail-5517-content-plan.md`](content/retail-5517-content-plan.md)
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
solid-rectangle rendering

native-text placement
coverage
interpolation
batching
atlas synchronization

Progress45 HUD background
Dialog4 HUD panels
Progress40 life
Progress41 mana
Progress42 skill
Progress46 stamina
Progress47 extended stamina
experience-bar rendering

10-button main-HUD action strip
all logical CMyButton frame states
alternate PK button skins

four main-HUD CMyCheck controls
both CMyCheck states
native check-control geometry and draw order
natural 32×32 check-control sprites

retail asset hashes
exact loose/package provenance where established
independent HUD reference geometry
exact real-driver framebuffer comparison
```

Current real-driver verification covers both supported logical resolutions against exact retail 5517
content on Apple M4 hardware using the OpenGL 4.1 Metal driver and an RGB565 logical target.

Graphics contracts and driver evidence are maintained in
[`compatibility/native-graphics.md`](compatibility/native-graphics.md).

Text contracts are maintained in [`compatibility/native-text.md`](compatibility/native-text.md).

## Content

Runtime content:

```text
content/retail-5517/payload
```

Current runtime closure: 58 files totaling 1,866,395 bytes.

It covers:

```text
ini/GameSetUp.ini
ini/info.ini

startup logos
ani/Control.ani

Progress45 HUD background
Dialog4 HUD panels

Progress40 life
Progress41 mana
Progress42 skill
Progress46 stamina
Progress47 extended stamina

10-button main-HUD action strip
Mission frames
Organise frames
four PK button skins

Check40 frames
Check43 frames
Check46 frames
Button411 frames
```

The experience bar uses solid-rectangle rendering and adds no image asset to the runtime closure.

Manifest source casing may differ from logical ANI casing when a loose retail file wins lookup.
Current example:

```text
ANI path:    data/main/ProgressForce2A.dds
source path: data/main/ProgressForce2a.dds
```

Retail `ini/package.ini` remains import configuration for resolving WDF-backed requirements from an
authorized retail source. It is not part of the curated runtime closure.

Production ANI frame requirements use `LooseThenPackage`. Rendering conformance may enforce a stricter
known retail provenance contract for a verified asset when native evidence establishes that the
specific file is loose-only or package-only in the audited 5517 source.

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

The current checked-in retail-5517 manifest records exactly 58 files and 1,866,395 bytes.

### Inspect Server.dat

```bash
dotnet run \
  --project tools/OpenConquer.Content.Tool \
  -- \
  inspect-server-dat \
  --file /path/to/Server.dat
```

## HUD Development Boundary

Current implemented main-HUD composition is:

```text
Progress45 background
Progress40 life
Progress41 mana
Progress46 stamina
Progress47 extended stamina
Dialog4 panels
Progress42 skill / experience
10-button action strip
four main-HUD CMyCheck controls
```

The action strip currently implements the verified native visual and interaction boundary:

```text
10 CMyButton controls
native control IDs
native draw order
46×22 logical hit regions
64×32 natural-size artwork
ANI frame modulo behavior
enabled / pressed / disabled state
single-button pointer capture
inside-release activation
outside-release cancellation
PK skin selection
PK timed state
Organise timed state
```

The returned action-button activation identities are intentionally not wired to downstream dialogs,
gameplay, or network operations until those feature boundaries are implemented from their own
native evidence.

The four implemented `CMyCheck` controls use the verified native boundary:

```text
Check40   0x3F4
Check43   0x3F7
Check46   0x3FF
Button411 0x3F8

22×22 logical hit rectangles
32×32 natural-size artwork
initial state 0
state/frame 0 ↔ 1
left-button down performs the state transition
no managed release rollback
```

The downstream map, screen-shift, equipment-view, and other parent-handler effects remain outside
this slice. The unresolved USER32 release/`BN_CLICKED` boundary must not be guessed.

Do not infer unimplemented side effects from ANI artwork names.

## Native Compatibility Discipline

Native 5517 evidence is authoritative for observable compatibility behavior.

When a verified native behavior is unusual, do not normalize it solely because a more conventional
modern implementation appears safer or cleaner.

Current examples include:

```text
skill-highlight DWORD timer sentinel and wrap behavior
signed negative experience-bar behavior
style-0 gauge source geometry and compatibility sampling
CMyButton release behavior after an external frame overwrite
ANI frame modulo behavior
CMyCheck state transition on mouse-down rather than mouse-up
CMyCheck low-byte setter truncation and bounds rejection
```

A deliberate deviation from verified native behavior must be explicit and documented rather than
introduced by a generic cleanup.

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

Development signing keys, release sequence state, and coordination locks remain outside repository
and product output.

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

Real-driver rendering conformance runs separately on supported desktop hardware with exact retail
content.

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
