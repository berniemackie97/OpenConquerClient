# Native Graphics Compatibility

Current compatibility contract for reconstructed Conquer Online 5517 graphics behavior. Detailed
native addresses, decompilation traces, and unresolved reverse-engineering evidence belong in the
native analysis notes.

## Logical Frame

Retail `ini/GameSetUp.ini` maps screen modes to:

| Modes | Logical size |
| ----- | -----------: |
| 0, 1  |      800×600 |
| 2, 3  |     1024×768 |

Desktop window size, fullscreen state, and presentation scaling do not change logical game
coordinates.

Retail prefers:

```text
D3DFMT_R5G6B5
D3DFMT_X1R5G5B5
```

OpenConquer requires an equivalent alpha-less 16-bit RGB logical target:

```text
RGB565 → R5 G6 B5
RGB5   → R5 G5 B5
```

Dithering is disabled.

Verified retail callers use `D3DFMT_D16`. OpenConquer therefore requires 16-bit depth with no
stencil.

Each logical frame starts with opaque black color and depth `1.0`.

## Presentation

Retail presentation uses one discard backbuffer, windowed presentation, and no presentation
interval.

OpenConquer separates logical rendering from the desktop framebuffer:

```text
logical target
    ↓
presentation transform
    ↓
desktop framebuffer
```

The verified outer client cadence is approximately 25 ms / 40 FPS.

OpenConquer preserves:

```text
wait only for remaining frame time
recheck after waiting
do not replay missed frames
overruns establish the next cadence anchor
gameplay/network/animation clocks remain independent
```

Retail can request multisampling. Logical-target multisampling remains unimplemented; the current
desktop host is single-sampled.

## Sprite Rendering

### Content Boundary

Verified ANI image dispatch currently supports:

```text
.tga → retail TGA decoder
.dds → verified single-level DXT3 decoder
```

Decoded content crosses into Rendering as top-left RGBA:

```text
retail TGA / DXT3 DDS
        ↓
OpenConquer.Content
        ↓
top-left RGBA
        ↓
OpenConquer.Rendering
        ↓
RGBA8 OpenGL texture
```

GPU sprite textures use:

```text
RGBA8
nearest filtering
clamp-to-edge
single mip level
```

Compressed DXT3 residency is native implementation machinery, not an observable compatibility
requirement.

### DDS Contract

The implemented DDS subset is:

```text
standard DDS header
2D texture
single level
DXT3 / BC2
explicit 4-bit alpha
RGB565 color endpoints
DXT3 four-color interpolation
partial edge-block clipping
no cubemaps
no volume textures
no mip chains
no trailing payload
```

Unsupported variants remain deferred until a verified consumer requires them.

### Default State

Sprite drawing uses:

```text
blend       = enabled
depth test  = disabled
depth write = disabled
culling     = disabled
```

Supported blend intent:

| Mode     | Source       | Destination            |
| -------- | ------------ | ---------------------- |
| Alpha    | source alpha | one minus source alpha |
| Additive | one          | one                    |

`Alpha` is the normal sprite default.

`Additive` is implemented for verified retail mode-1 consumers. The native `SRCCOLOR / ONE` branch
and draw-parameter-2 behavior remain outside the modern API until a production consumer is
established.

### Coordinates

Retail applies a Direct3D 8 half-pixel correction. OpenGL does not require it.

OpenConquer maps pixel edges directly:

```text
xNdc =  2 * x / width - 1
yNdc =  1 - 2 * y / height
```

Coordinates are top-left oriented. Geometry outside the logical target is clipped by the graphics
pipeline.

### Geometry

Supported sprite geometry:

```text
full texture → natural size
full texture → explicit size
source rectangle → explicit size
```

`SpriteSourceRectangle` must have non-negative coordinates, positive dimensions, and remain inside
the source texture.

Native pointer/sentinel APIs are represented by explicit modern operations.

### Color

`SpriteColor` is per-draw RGBA modulation:

```text
sampled texture × normalized SpriteColor
```

`SpriteColor.White` is `(255, 255, 255, 255)`.

### Rotation

Verified retail rotation uses:

```text
input              = signed integer degrees
reduction          = angle % 360
positive direction = clockwise in screen coordinates
pivot              = destination center
```

Ordering is:

```text
source rectangle
    ↓
destination size
    ↓
rotation
    ↓
logical coordinate transform
```

Native mutable sprite state is not reproduced.

## Static Main HUD Chrome

GFX-UI-001 implements the verified static background and panel chrome from the retail main HUD.

### Assets

`ani/Control.ani` contains:

```text
[Progress45]
FrameAmount=1
Frame0=data/main/ProgressBk.dds

[Dialog4]
FrameAmount=2
Frame0=data/main/mainDialog1.dds
Frame1=data/main/mainDialog2.dds
```

Verified retail identities:

```text
ani/Control.Ani
SHA-256 a1db47baaeb2f75eea3b5216378f97d713c2afeaefe05ec02e6f584794532d27

data/main/ProgressBk.dds
256×256
SHA-256 9b91a28e0170142a48dc03332691959eca01c179b9d8c592aa5b11af4f5d966c

data/main/mainDialog1.dds
256×256
SHA-256 505a4655c398e41bd25698b57caa50f48376cff713e1c8c6f287a03866038fe1

data/main/MainDialog2.dds
256×128
SHA-256 818d13f62509ac859fb0ed72d36ec6aeb2b12f9ed3dee3c6172171d99ba86f41
```

Verified source lookup:

```text
Control.Ani       → loose
ProgressBk.dds    → data.wdf
mainDialog1.dds   → data.wdf
mainDialog2.dds   → loose MainDialog2.dds override
```

Runtime import materializes the selected bytes into the curated content set; original WDF provenance
is not preserved in deployment.

### Layout

For logical height `H`:

```text
HUD origin Y = H - 141
```

Background:

```text
Progress45 frame 0
source: full 256×256
destination: (0, H - 141)
```

Panels:

```text
A
texture: Dialog4 frame 0
source:  (0, 112, 256, 144)
dest:    (0, H - 144)

B
texture: Dialog4 frame 0
source:  (0, 0, 256, 54)
dest:    (256, H - 53)

C
texture: Dialog4 frame 1
source:  (0, 0, 256, 54)
dest:    (512, H - 53)

D
texture: Dialog4 frame 1
source:  (0, 64, 256, 54)
dest:    (768, H - 53)
```

At `800×600`, panel D extends beyond the logical target and is clipped. It is not shrunk and no
HUD-specific scissor is applied.

All five draws use:

```text
color    = white
blend    = alpha
rotation = 0
```

No resolution scaling is applied to HUD coordinates.

### Ordering

The verified native HUD pass orders major groups as:

```text
ranges / Flash
background
HP / MP / stamina
static panels
skill / XP
controls / overlays
```

GFX-UI-001 implements only the background and static-panel slots. The renderer intentionally exposes
them separately so later slices can preserve native interleaving.

### Failure Behavior

Verified native behavior distinguishes the two asset groups:

```text
Progress45 unavailable
→ skip background
→ continue HUD pass

Dialog4 unavailable
→ stop the remaining HUD pass
```

Malformed ANI or decoded image data is rejected rather than silently accepted.

The native outer HUD gate has not yet been reconstructed separately and is outside this static
slice.

## Real-Driver Conformance

Conformance uses the production OpenGL path on a real graphics driver.

Verified development driver:

```text
OpenGL:   4.1 Metal - 90.5
GLSL:     4.10
Vendor:   Apple
Renderer: Apple M4
Target:   RGB565
```

### Sprite Baselines

| Case                         | SHA-256                                                            |
| ---------------------------- | ------------------------------------------------------------------ |
| Natural RGB565               | `93939cf5e51ea6298b729836b80561627550505e6f0771588082c5a33142833c` |
| Natural RGB555               | `313ec6083e2eb72c7e3e63594859225c09bee573d155afa9e24d0399fd203ed7` |
| Whole-texture stretch RGB565 | `45098e61451897bda7b976fc8d55b749ac5d327c1739e9e5fcbc249848b37340` |
| Source-region stretch RGB565 | `f4d724a2e3703eec53fa10df55f64da31c59b706db5db41a00bd8592eb9cbbfd` |
| RGBA modulation RGB565       | `3fc3d1e606877135ff6296dd684e01d77756917cf05cbc7a756765f6e50eb1b7` |
| Additive blend RGB565        | `f7f9e13d8ace3958b3fee2a2cbfa1d16dc90523b4ea4fd124c8e3aba6a872401` |
| Rotation RGB565              | `56f3bd55bec37797ec2e6b30f9db8daf8a8347417ee8a09fb86d59cb952e17f7` |

### Retail DXT3 Probe

Verified fixture:

```text
ani/weather.ani
    ↓
[YinFa1] Frame0
    ↓
data.wdf / data/firework/yinfa1/1.dds
```

Encoded SHA-256:

```text
1a79bb1faf0c94b759d723a18b9ef6908e38a0c7f5e600ceadb3675ecb7ea2df
```

Decoded RGBA SHA-256:

```text
883de947994f7866531817efc02f2aedd8dfeb0ac7489683d21ec9dea3f05624
```

Observed Apple M4 RGB565 framebuffers:

```text
additive:
286c83f89306b178692db40f6fa26c3cc2220b7cfd727a69986efb38949f2cdb

alpha:
cd6f0b1244ce58dfce7c2e705f17dc491b84f2e98d367edee5bce1cbc83765bc
```

Production DDS decode is compared byte-for-byte with an independent DXT3 reference decoder.

### Main HUD Probe

HUD conformance verifies:

```text
exact retail asset hashes
verified loose/package provenance
production DDS decode == independent DXT3 decode
production MainHudChromeRenderer
        ==
independently specified native draw sequence
```

The reference draw path does not use `MainHudChromeLayout`.

Observed Apple M4 RGB565 logical framebuffer hashes:

```text
800×600
bf905c1d0fdadf486303ba4849221bec836b8a6f52f6b23a4d91b09244cd8cf1

1024×768
dbdaa4cd332fda6661d438ad5b29b97d051ec2d51cc8149b92a2e4aede4fb629
```

These hashes document the verified driver result. The portable conformance requirement is equality
between the production HUD renderer and the independently specified draw sequence on the active
supported logical target.

## Host Framebuffer

The host framebuffer is presentation-only:

```text
logical RGB565/RGB5 + D16
    ↓
framebuffer blit
    ↓
desktop framebuffer
    ↓
platform swap
```

Requirements:

```text
single-sampled host framebuffer
sRGB conversion disabled before blit
scissor cannot clip presentation
zero-sized framebuffer allowed while minimized
host resize does not alter logical coordinates
```

Rendering owns presentation. Platform owns the window, OpenGL context, physical framebuffer state,
and swap.

## Current Scope

Implemented and verified:

```text
800×600 and 1024×768 logical rendering
RGB565 / RGB555-compatible logical color
D16 depth
25 ms outer frame cadence

TGA and single-level DXT3 ANI frame decoding
WDF-backed DDS lookup
top-left RGBA Content-to-Rendering boundary

natural-size sprites
stretching
source rectangles
nearest filtering
RGBA modulation
alpha blending
verified additive blending
integer-degree rotation
logical-target clipping

static Progress45 HUD background
static Dialog4 HUD panel chrome
native HUD placement at both supported resolutions
native background/panel ordering slots
real-driver HUD framebuffer conformance

real-driver sprite framebuffer conformance
independent retail DXT3 decode conformance
```

Remaining:

```text
logical-target multisampling
ANI runtime progression/timing
unverified SRCCOLOR/ONE consumer behavior
native draw-parameter-2 consumer verification
sprite batching
higher-level texture caching
remaining HUD gauges, skill/XP, controls, overlays, and HUD gate
map, role, effect, and animation integration
additional image variants only when required by verified consumers
```

## Modernization Boundary

Preserve observable 5517 behavior, not obsolete implementation machinery:

```text
preserve logical coordinates and framebuffer precision
use correct OpenGL pixel-edge mapping instead of D3D8 half-pixel correction
replace pointer/sentinel APIs with explicit operations
replace packed mutable sprite color with SpriteColor
replace native draw integers with verified rendering semantics
normalize supported retail images to RGBA at the Content boundary
preserve HUD draw order without building a monolithic HUD renderer
do not preserve WDF deployment topology when curated bytes preserve behavior
do not implement unverified image formats or sprite modes speculatively
```
