# Native Graphics Compatibility

Current compatibility contract for reconstructing Conquer Online 5517 graphics behavior. Detailed
native addresses, decompilation traces, caller inventories, and unresolved reverse-engineering
evidence belong in the native analysis notes.

## Logical Frame

### Resolution

Retail `ini/GameSetUp.ini` maps screen modes to:

| Modes | Logical size |
| ----- | -----------: |
| 0, 1  |      800×600 |
| 2, 3  |     1024×768 |

Desktop window size, fullscreen state, and presentation scaling do not change logical game
coordinates.

### Color Target

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

Retail also contains an `X8R8G8B8` device-creation fallback. Generic OpenGL failure is not treated
as equivalent evidence for selecting that path.

### Depth

Verified retail callers use `D3DFMT_D16`.

OpenConquer requires:

```text
16-bit depth
no stencil
```

The dormant retail `D24S8` path is outside the verified 5517 contract.

### Clear

Each logical frame starts with:

```text
color = opaque black
depth = 1.0
```

## Presentation

Retail presentation uses:

```text
BackBufferCount      = 1
SwapEffect           = DISCARD
Windowed             = TRUE
PresentationInterval = 0
```

OpenConquer keeps logical rendering independent from desktop presentation:

```text
logical target
    ↓
presentation transform
    ↓
desktop framebuffer
```

The outer retail client frame pipeline is gated at approximately 25 ms / 40 FPS.

OpenConquer preserves:

```text
wait only for remaining frame time
recheck time after waiting
do not replay missed frames
overruns establish the next cadence anchor
gameplay/network/animation clocks remain independent
```

Retail can request 2×, 4×, or 8× multisampling depending on configuration and device support.
Logical-target multisampling is not implemented yet; the current desktop host therefore requests a
single-sampled framebuffer.

## Sprite Rendering

### Texture Contract

The verified TGA framebuffer fixture uses:

```text
ani/Common.Ani
└── [Syndicate]
    └── Frame0=data/pic/Syndicate.tga
```

Verified frame:

```text
size:        14×14
TGA type:    10
pixel depth: 32-bit
source:      BGRA
decoded:     top-left RGBA
```

Hashes:

```text
encoded:
a813875f120d20908e13c5cdb4410008d5ff1b6f2d6f9186051185f7aa331b3a

decoded RGBA:
1e112db318ecd33cba4b2980d0ed92e502e74bcd0e6a539747733cad718f8c37
```

The verified retail DXT3 firework fixture uses:

```text
ani/weather.ani
└── [YinFa1]
    ├── FrameAmount=9
    └── Frame0=data/firework/yinfa1/1.dds
```

Verified frame 0:

```text
storage:     data.wdf
size:        384 bytes
dimensions:  16×16
DDS format:  single-level DXT3
pixel flags: DDPF_FOURCC only
FourCC:      DXT3
```

Encoded SHA-256:

```text
1a79bb1faf0c94b759d723a18b9ef6908e38a0c7f5e600ceadb3675ecb7ea2df
```

OpenConquer's supported DDS contract is deliberately narrower than the native decoder:

```text
standard DDS header
2D texture
single level
DXT3 / BC2 encoding
explicit 4-bit alpha
RGB565 color endpoints
DXT3 four-color interpolation
partial edge-block clipping
no cubemaps
no volume textures
no mip chains
no trailing levels or payload
```

Verified retail FourCC files contain legacy values in fields that are irrelevant when `DDPF_FOURCC`
is selected. Those unused RGB and reserved fields are therefore not interpreted as
uncompressed-pixel metadata.

ANI frame decoding dispatches supported image formats explicitly:

```text
.tga → retail TGA decoder
.dds → retail DXT3 DDS decoder
```

Other image extensions remain unsupported until consumer evidence requires them.

The Content-to-Rendering boundary is normalized RGBA:

```text
retail TGA / DXT3 DDS
        ↓
OpenConquer.Content
        ↓
top-left RGBA pixels
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

The native client can preserve DXT3 compression through texture upload when the device supports it.
That is native implementation machinery rather than an observable compatibility requirement.
OpenConquer preserves decoded pixels and rendering behavior instead of exposing DDS, DXT3, BC2, or
compressed OpenGL representation across the Content-to-Rendering boundary.

TGA and DDS support remain limited to formats required by verified retail consumers.

### Default State

Sprite drawing uses:

```text
blend       = enabled
depth test  = disabled
depth write = disabled
culling     = disabled
```

Blend mode is explicit rendering intent:

| `SpriteBlendMode` | Source       | Destination            | Verified basis                  |
| ----------------- | ------------ | ---------------------- | ------------------------------- |
| `Alpha`           | source alpha | one minus source alpha | retail default                  |
| `Additive`        | one          | one                    | reachable retail mode-1 callers |

Existing sprite APIs default to `Alpha`.

`Additive` represents the resolved rendering behavior required by verified retail mode-1 firework
paths. OpenConquer does not expose the native integer draw parameter or Direct3D format predicate.

The verified mode-1 firework callers use:

```text
full texture
natural dimensions
white RGB modulation
alpha = 255
DXT3 source content
ONE / ONE blending
```

The native client may retain DXT3 as compressed GPU content for this path. OpenConquer instead
decodes the same DXT3 pixels to RGBA8 before rendering; compressed residency itself is not part of
the compatibility contract.

The native mode-1 `SRCCOLOR / ONE` branch is proven at the primitive level but has no verified
retail production consumer yet, so it is not implemented.

Native draw parameter 2 is also proven at the primitive level:

```text
SRCALPHA / INVSRCALPHA
RGB-only color writes
fixed all-channel write-mask restoration afterward
```

No reachable retail mode-2 consumer, target, or observable framebuffer requirement has been
verified. Mode 2 therefore remains deferred and is not part of the modern Rendering API.

### Coordinates

Retail applies a Direct3D 8 `-0.5` screen-space correction. That rasterization workaround is not
carried into OpenGL.

OpenConquer maps logical pixel edges directly:

```text
xNdc =  2 * x / width - 1
yNdc =  1 - 2 * y / height
```

Coordinates are top-left oriented. Out-of-bounds geometry is clipped by the graphics pipeline.

### Source Regions and Stretching

`SpriteSourceRectangle` requires:

```text
X >= 0
Y >= 0
Width > 0
Height > 0
region contained by texture
```

Supported geometry:

```text
full texture → natural size
full texture → explicit destination size
source region → explicit destination size
```

Source rectangles affect UVs only. Destination dimensions independently establish geometry.

Native `RECT*`, null-pointer, and zero-dimension sentinel behavior is represented by explicit modern
operations.

The currently verified additive capability is exposed only for full-texture natural-size drawing.
Additive combinations with source rectangles, stretching, or rotation are not exposed without
verified consumer evidence.

### Color

`SpriteColor` explicitly represents per-draw RGBA modulation:

```text
SpriteColor(Red, Green, Blue, Alpha)
SpriteColor.White = (255, 255, 255, 255)
```

Fragment output is:

```text
sampled texture × normalized SpriteColor
```

The selected blend mode is then applied.

Rendering does not retain packed Direct3D diffuse colors or mutable native sprite-color state.

### Rotation

Verified retail rotation:

```text
input              = signed integer degrees
angle reduction    = angle % 360
positive direction = clockwise in screen coordinates
pivot              = destination center
```

Ordering:

```text
source region
    ↓
destination size / stretch
    ↓
rotate about destination center
    ↓
logical pixel → OpenGL coordinates
```

Rotation preserves UVs, color, and texture selection.

Native `Sprite_Rotate` mutates current vertices, but all seven recovered rotating callers rebuild
destination geometry first. OpenConquer therefore models rotation explicitly per draw.

An original angle of `0` uses the unrotated path. Other values execute signed modulo reduction.

CPU-specific native approximation/dispatch behavior is implementation machinery, not a portable
compatibility requirement.

## Real-Driver Conformance

Conformance renders through the production OpenGL path and compares logical framebuffer bytes
against independent expectations.

Verified Apple M4 driver:

```text
OpenGL:   4.1 Metal - 90.5
GLSL:     4.10
Vendor:   Apple
Renderer: Apple M4
Target:   RGB565
```

### Stable Baselines

| Case                         | SHA-256                                                            |
| ---------------------------- | ------------------------------------------------------------------ |
| Natural RGB565               | `93939cf5e51ea6298b729836b80561627550505e6f0771588082c5a33142833c` |
| Natural RGB555               | `313ec6083e2eb72c7e3e63594859225c09bee573d155afa9e24d0399fd203ed7` |
| Whole-texture stretch RGB565 | `45098e61451897bda7b976fc8d55b749ac5d327c1739e9e5fcbc249848b37340` |
| Source-region stretch RGB565 | `f4d724a2e3703eec53fa10df55f64da31c59b706db5db41a00bd8592eb9cbbfd` |
| RGBA modulation RGB565       | `3fc3d1e606877135ff6296dd684e01d77756917cf05cbc7a756765f6e50eb1b7` |
| Additive blend RGB565        | `f7f9e13d8ace3958b3fee2a2cbfa1d16dc90523b4ea4fd124c8e3aba6a872401` |
| Rotation RGB565              | `56f3bd55bec37797ec2e6b30f9db8daf8a8347417ee8a09fb86d59cb952e17f7` |

Explicit white modulation remains byte-identical to the natural RGB565 baseline.

### Additive Probe

The synthetic additive probe deliberately distinguishes `ONE / ONE` from both other plausible blend
pairs:

```text
destination = opaque blue
source      = opaque-white texture × (255, 0, 0, 128)

ONE / ONE
→ (255, 0, 255)

SRCALPHA / ONE
→ reduced red + full blue

SRCALPHA / INVSRCALPHA
→ reduced red + reduced blue
```

Verified framebuffer:

```text
RGBA = (255, 0, 255, 255)

SHA-256:
f7f9e13d8ace3958b3fee2a2cbfa1d16dc90523b4ea4fd124c8e3aba6a872401
```

This remains the exact blend-state oracle for `SpriteBlendMode.Additive`.

### Retail DXT3 Firework Probe

The retail DXT3 probe verifies the production compatibility path rather than only synthetic blend
state:

```text
ani/weather.ani
    ↓
[YinFa1] Frame0
    ↓
data.wdf / data/firework/yinfa1/1.dds
    ↓
production ANI → DDS decoder
    ↕ byte-exact comparison
independent DXT3 reference decoder
    ↓
RGBA8 OpenGL texture
    ↓
full texture / natural size / SpriteColor.White
    ↓
SpriteBlendMode.Additive
```

Verified decoded RGBA SHA-256:

```text
883de947994f7866531817efc02f2aedd8dfeb0ac7489683d21ec9dea3f05624
```

On the verified Apple M4 RGB565 target:

```text
additive framebuffer:
286c83f89306b178692db40f6fa26c3cc2220b7cfd727a69986efb38949f2cdb

alpha framebuffer:
cd6f0b1244ce58dfce7c2e705f17dc491b84f2e98d367edee5bce1cbc83765bc
```

These framebuffer hashes document the observed verified-driver result rather than defining a
portable cross-driver baseline.

The conformance requirement is:

```text
production decoded RGBA == independent DXT3 reference decode
additive framebuffer != alpha framebuffer
```

The synthetic additive probe separately pins the exact `ONE / ONE` blend behavior.

### Rotation Probe

```text
texture:       blue | red | green | yellow
source region:       red | green
destination:   4×2 at (2, 2)
angle:         1,440,000,090° → 90°
pivot:         destination center
target:        8×6
```

Expected clockwise output:

```text
........
...RR...
...RR...
...GG...
...GG...
........
```

This verifies signed reduction, clockwise orientation, center pivot, stretch-before-rotation
ordering, source UV preservation, and the production rotation path.

## Host Framebuffer

The desktop framebuffer is presentation-only:

```text
logical RGB565/RGB5 + D16
    ↓
OpenGL framebuffer blit
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
800×600 / 1024×768 logical rendering
RGB565 / RGB555-compatible logical color
D16 depth
25 ms outer frame cadence

ANI TGA frame dispatch and decoding for verified retail content
ANI DXT3 DDS frame dispatch and decoding for verified retail content
single-level DXT3 / BC2 pixel decoding
explicit-alpha DXT3 semantics
RGB565 endpoint expansion
partial DXT3 block-edge clipping
package-backed WDF DDS resolution
normalized top-left RGBA Content-to-Rendering boundary

top-left RGBA sprite textures
natural-size drawing
stretching
source regions
nearest sampling
RGBA modulation
alpha blending
additive blending for the verified natural-size consumer contract
integer-degree rotation
logical-target clipping

real-driver framebuffer conformance
independent retail DXT3 decode conformance
verified retail DXT3 additive rendering
```

Remaining:

```text
logical-target multisampling
ANI runtime progression/timing
unverified mode-1 SRCCOLOR/ONE consumer behavior
native draw-parameter-2 consumer verification
sprite batching
higher-level texture caching
additional DDS variants only when verified consumers require them
additional required TGA variants
map/UI/role/effect/animation integration
```

## Modernization Boundary

Preserve observable 5517 behavior, not obsolete implementation machinery:

```text
preserve logical coordinates and framebuffer precision
replace D3D8 half-pixel correction with correct OpenGL mapping
replace RECT*/sentinel APIs with explicit operations
replace packed/mutable sprite color with per-draw SpriteColor
replace native draw integers with verified rendering semantics
replace mutable C3Sprite rotation history with explicit per-draw rotation
normalize supported retail image formats to RGBA at the Content boundary
do not expose DDS/DXT3/BC2 or compressed GPU representation without a proven requirement
do not propagate D3DFORMAT values without a proven compatibility requirement
do not implement unverified sprite-mode combinations speculatively
do not implement additional image formats without verified consumer evidence
do not emulate CPU-specific native math dispatch without a compatibility requirement
```
