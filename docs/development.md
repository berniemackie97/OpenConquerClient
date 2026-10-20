# Development

OpenConquer Client targets the SDK pinned by [`global.json`](../global.json).

Reference:

- [`architecture/architecture.md`](architecture/architecture.md)
- [`compatibility/native-graphics.md`](compatibility/native-graphics.md)
- [`compatibility/native-text.md`](compatibility/native-text.md)
- [`content/retail-5517-content-plan.md`](content/retail-5517-content-plan.md)
- [`architecture/launcher-managed-installation.md`](architecture/launcher-managed-installation.md)
- [`architecture/launcher-play-screen.md`](architecture/launcher-play-screen.md)

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

Builds treat warnings as errors and use the repository's analyzer configuration.

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

`ini/GameSetUp.ini` selects the logical render size. The desktop window size and the presentation
mode are set independently of it.

## Rendering Conformance

Build:

```bash
dotnet build tests/Conformance/OpenConquer.Rendering.Conformance/OpenConquer.Rendering.Conformance.csproj \
  -c Release \
  --no-restore
```

Run against an unmodified retail 5517 root:

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

10-slot quickbar/grid
quickbar fixed and parametric content families
quickbar hover / activation / pickup state

Magic0 selected-skill image
Image0 selected-skill cover
selected-skill cooldown text
stateful cooldown/cover ordering
production OpenGL text-context façade

retail asset hashes
exact loose/package provenance where established
independent HUD reference geometry
exact real-driver framebuffer comparison
```

Real-driver testing currently covers both supported logical resolutions with unmodified retail 5517
content on Apple M4 hardware, using the OpenGL 4.1 Metal driver and an RGB565 logical target.

Graphics rules and per-driver results are documented in
[`compatibility/native-graphics.md`](compatibility/native-graphics.md).

Text rendering rules are documented in
[`compatibility/native-text.md`](compatibility/native-text.md).

## Content

Runtime content:

```text
content/retail-5517/payload
```

Current runtime closure: 3,785 files totaling 21,319,841 bytes.

It covers:

```text
ini/GameSetUp.ini
ini/info.ini
ini/Font.ini

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

ani/Magic.ani
ani/ItemMinIcon.Ani
ani/effect.ani
10-slot quickbar/grid fixed artwork
action / dance families
magic / XP-magic families
item-min-icon families
verified glow families

Magic0 selected-skill image
Image0 selected-skill cover

category-8 status-hint resources
ini/MagicType.dat
ini/MagicEffect.ini
ini/SubProfessionInfo.ini
ini/Cn_Res.ini
ini/StrRes.ini
category-9 Magic hints
```

The experience bar uses solid-rectangle rendering and adds no image asset to the runtime closure.

The casing of a manifest source path can differ from the logical ANI path when a loose retail file
wins the lookup. Current example:

```text
ANI path:    data/main/ProgressForce2A.dds
source path: data/main/ProgressForce2a.dds
```

Retail `ini/package.ini` is import configuration only: the importer uses it to resolve WDF-backed
requirements from an authorized retail source. It is not part of the curated runtime closure.

Production ANI frame requirements use `LooseThenPackage`. Rendering conformance can be stricter for
a single asset: when research on the retail client shows that the file exists only loose, or only
inside a package, in the 5517 source, the conformance test can require that provenance.

Compatibility-only assets do not expand the runtime closure.

`Server.dat` is reference material for offline tooling only and must not ship with the client.

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

The check requires all three to match:

```text
ClientContentClosure
        ==
manifest
        ==
payload
```

The checked-in retail-5517 manifest currently records 3,785 files and 21,319,841 bytes.

### Inspect Server.dat

```bash
dotnet run \
  --project tools/OpenConquer.Content.Tool \
  -- \
  inspect-server-dat \
  --file /path/to/Server.dat
```

## HUD Development Boundary

The main HUD currently draws these elements:

```text
Progress45 background
Progress40 life
Progress41 mana
Progress46 stamina
Progress47 extended stamina
Dialog4 panels
Progress42 skill / experience
10-slot quickbar/grid
10-button action strip
four main-HUD CMyCheck controls
selected-skill image / previously armed cover
selected-skill cooldown text
category-8 main-HUD status hints
category-9 Magic hints
```

The action strip reproduces the retail `CMyButton` behavior:

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

Button activations are returned to the caller but are not yet connected to dialogs, gameplay or
network operations. Each will be connected when that feature is implemented, based on its own
research on the retail client.

The four `CMyCheck` controls reproduce the retail behavior:

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

GFX-UI-006 implements the ten-slot quickbar/grid:

```text
control ID 0x3FD
1×10 slots
local origin (90,98)
40×40 visual cells
41-pixel horizontal input stride

item
action
magic
XP magic
dance
weapon swap

hover / activation / pickup state
quantity / upgrade / cover / cooldown / glow rendering
```

The quickbar content closure includes broad parametric ANI families on purpose, because the set is
expanded from what the quickbar can display. Do not replace it with a hand-picked subset of assets.

GFX-UI-007 implements the selected-skill control:

```text
control ID 0x3FE
initial section Magic0
local origin (753,96)
47×46 destination
50×50 selected source
64×64 Image0 cover source
```

Selected-skill cooldown rendering is a separate step owned by the Client project. The image draw
uses the cover flag as it was last armed. The cooldown routine then clears that flag, draws the
cooldown number when a cooldown is active, and re-arms the flag for the next image draw.

Keep this stateful sequence. Do not replace it with a stateless overlay order.

In production, cooldown text takes its font and code-page configuration from Content and draws
through an `OpenGLTextContext` owned by Rendering. Rendering owns the lifetimes of host font
discovery, FreeType, the rasterizer, layout and caching, and the CPU and GPU atlases.

Gameplay does not supply live quickbar contents, the selected skill or cooldown times yet. The map,
screen-shift, equipment-view, action-dialog and network effects that these controls lead to are not
implemented either.

GFX-UI-008 implements the category-8 main-HUD status-hint panel: the Dialog21 backdrop, StrRes.ini
string resources, Win32 RGNDATA hotspot regions, the retail hotspot selection, the MP startup guard
and normal-font text rendering. Region hit testing uses CDlgMain-local pixels, while hint anchors
are relative to the shell. The panel is drawn after the selected-skill cooldown text.

Category-9 Magic hints cover both the learned-magic and the zero-magic case. They use retail magic,
effect and subprofession metadata, configured keyed localization, the retail client's wrapping of
encoded bytes, colored text and a 12-pixel font. Real-driver conformance covers 800x600 and
1024x768. Runtime snapshots are supported, but live Gameplay producers are not connected yet.

Per-control tooltips, other status-hint categories and the outer HUD gate are not implemented yet.

Do not infer unimplemented side effects from ANI artwork names.

## Native Compatibility Discipline

For observable behavior, the retail 5517 client, as documented by research, is the reference.

If a confirmed 5517 behavior is unusual, keep it. Do not change it only because a more conventional
modern implementation looks safer or cleaner.

Current examples:

```text
skill-highlight DWORD timer sentinel and wrap behavior
signed negative experience-bar behavior
style-0 gauge source geometry and compatibility sampling
CMyButton release behavior after an external frame overwrite
ANI frame modulo behavior
CMyCheck state transition on mouse-down rather than mouse-up
CMyCheck low-byte setter truncation and bounds rejection
quickbar parametric ANI-family closure expansion
selected-skill mutation before later ANI lookup
selected-skill cover flag surviving image clear
cooldown cover flag applying to the following image draw
```

Any intended deviation from confirmed 5517 behavior must be documented as such. It must not slip in
as part of a general cleanup.

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

Development signing keys, release sequence state and coordination locks are kept out of the
repository and the product output.

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

Real-driver rendering conformance runs separately, on supported desktop hardware with unmodified
retail content.

GitHub Actions dependencies must stay pinned to commit SHAs.

## Commit Rule

A change is complete only after:

```text
implementation
focused tests
documentation
native / real-driver verification when applicable
full quality gate
final re-audit
```
