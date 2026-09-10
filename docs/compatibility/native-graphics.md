# Native Graphics Compatibility

Verified retail 5517 graphics behavior that constrains OpenConquer Client. Detailed
reverse-engineering evidence remains in the native analysis notes.

## Logical Resolution

| Retail mode | Logical size | Retail shell            |
| ----------: | -----------: | ----------------------- |
|           0 |      800×600 | Windowed                |
|           1 |      800×600 | Display-mode fullscreen |
|           2 |     1024×768 | Windowed                |
|           3 |     1024×768 | Display-mode fullscreen |

Source:

```ini
[ScreenMode]
ScreenModeRecord=<value>
```

from `ini/GameSetUp.ini`.

OpenConquer preserves the logical size:

```text
0 or 1 → 800×600
2 or 3 → 1024×768
```

Desktop window size and mode are modern host policy and do not change logical game coordinates.

## Logical Render Target

### Color

Retail probes:

```text
1. D3DFMT_R5G6B5
2. D3DFMT_X1R5G5B5
```

OpenGL mirrors that preference:

```text
1. RGB565
2. RGB5
```

Accepted precision:

```text
RGB565 → R5 G6 B5 A0
RGB5   → R5 G5 B5 A0
```

Dithering is disabled because retail uses `D3DRS_DITHERENABLE = FALSE`.

Retail contains a later `D3DFMT_X8R8G8B8` fallback after Direct3D device-creation failure.
OpenConquer does not treat generic OpenGL render-target failure as equivalent because the native
trigger is different.

### Depth

Reachable retail paths use:

```text
D3DFMT_D16
```

OpenConquer therefore requires an exact 16-bit depth attachment and no stencil attachment.

The dormant retail `D24S8` branch is not reachable from verified 5517 callers.

### Frame Clear

Each logical frame begins with:

```text
color = opaque black
depth = 1.0
```

Rendering establishes deterministic clear state before clearing.

## Presentation

Verified retail presentation:

```text
BackBufferCount       = 1
SwapEffect            = DISCARD
Windowed              = TRUE
Depth                 = D16
PresentationInterval  = 0
```

Retail uses a windowed Direct3D device for all four screen modes. Modes 1 and 3 alter desktop/window
state rather than using a fullscreen D3D swap chain.

OpenConquer keeps VSync disabled, renders to the fixed logical target, then presents that target
into the physical host framebuffer separately.

### Frame Cadence

Retail gates the outer client frame pipeline at:

```text
25 ms
40 FPS maximum
```

OpenConquer preserves that cadence using monotonic time.

Contract:

```text
wait only for the remaining interval
recheck elapsed time after waiting
do not replay missed frames
overruns establish the next cadence anchor
gameplay/network/animation clocks remain independent
```

## Multisampling

Retail can select 2×, 4×, or 8× multisampling depending on graphics-detail configuration and format
support.

Logical-target multisampling is not implemented yet.

The desktop host therefore requests zero framebuffer samples so backend defaults cannot alter
presentation behavior.

## Sprite Rendering

### Verified Asset

```text
ani/Common.Ani
└── [Syndicate]
    └── Frame0=data/pic/Syndicate.tga
```

Fixture:

```text
size:        14×14
TGA type:    10 (RLE true color)
pixel depth: 32-bit
descriptor:  0x08
source:      BGRA
output:      top-left RGBA
```

Hashes:

```text
encoded TGA:
a813875f120d20908e13c5cdb4410008d5ff1b6f2d6f9186051185f7aa331b3a

decoded RGBA:
1e112db318ecd33cba4b2980d0ed92e502e74bcd0e6a539747733cad718f8c37
```

Current TGA support is intentionally limited to the verified format required by this path.

### Default State

Retail default sprite rendering uses:

```text
alpha blending = enabled
depth test     = disabled
depth write    = disabled
culling        = disabled

source blend      = SRCALPHA
destination blend = INVSRCALPHA
```

OpenGL equivalent:

```text
BlendEquation = Add
BlendFunc      = SrcAlpha, OneMinusSrcAlpha
```

Textures use:

```text
RGBA8
nearest min/mag filtering
clamp-to-edge
single mip level
```

### Coordinates

Retail Direct3D 8 applies a `-0.5` screen-space correction. That D3D rasterization workaround is not
carried into OpenGL.

OpenGL uses integer logical pixel edges:

```text
left   =  2 * x / targetWidth - 1
right  =  2 * (x + width) / targetWidth - 1
top    =  1 - 2 * y / targetHeight
bottom =  1 - 2 * (y + height) / targetHeight
```

Texture coordinates remain top-left oriented.

Destination geometry may extend outside the logical target and is clipped by the graphics pipeline.

### Source Regions and Stretching

Retail supports selecting a source region independently from its destination size.

OpenConquer represents that behavior with:

```text
SpriteSourceRectangle
├── X
├── Y
├── Width
└── Height
```

Valid regions require:

```text
X >= 0
Y >= 0
Width > 0
Height > 0
X + Width <= texture width
Y + Height <= texture height
```

Public operations:

```text
whole texture → natural size
whole texture → explicit destination size
source region → explicit destination size
```

This preserves observable retail behavior without carrying forward Win32 `RECT*`, null-pointer, or
zero-dimension sentinel APIs.

UV mapping:

```text
u0 = X / textureWidth
v0 = Y / textureHeight
u1 = (X + Width) / textureWidth
v1 = (Y + Height) / textureHeight
```

Destination width and height are independent from source size and must be positive.

No verified 5517 caller currently requires negative, reversed, empty, or out-of-texture source
regions.

### Color Modulation

Retail sprite color is applied uniformly across the sprite vertices before texture blending.

OpenConquer represents the color explicitly as RGBA byte channels:

```text
SpriteColor
├── Red
├── Green
├── Blue
└── Alpha
```

Neutral modulation is:

```text
SpriteColor.White = (255, 255, 255, 255)
```

The fragment operation is equivalent to:

```text
output RGBA = sampled texture RGBA × normalized sprite RGBA
```

The result then enters the existing source-alpha blend path.

Existing colorless draw operations explicitly use `SpriteColor.White`, preserving their previous
output. Explicit-color overloads exist for natural-size, stretched, and source-region draws.

Rendering does not preserve native mutable sprite-color state or expose packed Direct3D color
values. A legacy zero-value sentinel or retained-color behavior belongs at a compatibility consumer
boundary if a verified caller requires it.

## Sprite Conformance

Real-driver conformance uses retail-compatible RGB565/RGB555 logical targets and reads the
production framebuffer back byte-for-byte.

### Natural Size

```text
source:      full 14×14 Syndicate frame
destination: 14×14
position:    (7, 9)
```

Canonical framebuffer hashes:

```text
RGB565:
93939cf5e51ea6298b729836b80561627550505e6f0771588082c5a33142833c

RGB555:
313ec6083e2eb72c7e3e63594859225c09bee573d155afa9e24d0399fd203ed7
```

Historical RGBA8 evidence:

```text
fe8e6998cad4c6399059d43ec465837f53a346878f8d69e200bc7005a348a459
```

Production renders into the 16-bit logical target.

### Whole-Texture Stretch

```text
source:      full 14×14 texture
destination: 20×18
position:    (2, 2)
```

Verified Apple M4 RGB565 hash:

```text
45098e61451897bda7b976fc8d55b749ac5d327c1739e9e5fcbc249848b37340
```

### Source-Region Stretch

```text
source:      (1, 1), 12×12
destination: 20×16
position:    (6, 8)
```

Verified Apple M4 RGB565 hash:

```text
f4d724a2e3703eec53fa10df55f64da31c59b706db5db41a00bd8592eb9cbbfd
```

Stretch conformance independently derives nearest-neighbor source selection from destination pixel
centers and compares the complete GPU readback with the expected framebuffer.

### Color

Explicit white modulation must produce the exact canonical natural-size framebuffer:

```text
RGB565:
93939cf5e51ea6298b729836b80561627550505e6f0771588082c5a33142833c
```

The non-white probe uses:

```text
texture:  1×1 opaque white
color:    RGBA (255, 128, 64, 128)
target:   three pixels
paths:    natural / stretch / source region
```

Verified Apple M4 RGB565 framebuffer hash:

```text
3fc3d1e606877135ff6296dd684e01d77756917cf05cbc7a756765f6e50eb1b7
```

This verifies RGB modulation, alpha modulation, source-alpha blending, all three explicit-color draw
operations, and fixed-point RGB565 output through the production OpenGL path.

### Verified Driver

```text
OpenGL:   4.1 Metal - 90.5
GLSL:     4.10
Vendor:   Apple
Renderer: Apple M4
Target:   RGB565
```

## Host Framebuffer

The physical framebuffer is presentation-only:

```text
logical target
├── RGB565 preferred / RGB5 fallback
└── D16 depth
        │
        ▼
OpenGL framebuffer blit
        │
        ▼
desktop framebuffer
        │
        ▼
platform buffer swap
```

Rendering owns the logical-to-host blit. Platform owns the window, OpenGL context, framebuffer size,
and swap.

Current presentation requirements:

```text
host framebuffer is single-sampled
GL_FRAMEBUFFER_SRGB disabled before blit
scissor cannot clip presentation
zero-sized host framebuffer valid while minimized
host resize does not change logical coordinates or recreate logical target
```

## Current Scope

Verified and implemented:

```text
800×600 and 1024×768 logical rendering
RGB565 / RGB555-compatible color precision
D16 depth
25 ms outer frame cadence
default sprite alpha blending
top-left RGBA textures
natural-size sprite drawing
whole-texture stretching
source-region selection and stretching
nearest sprite sampling
per-draw RGBA sprite modulation
logical-target clipping
real-driver framebuffer conformance
```

Not yet verified or implemented:

```text
logical-target multisampling
ANI runtime frame progression and timing
draw parameters 1 and 2
sprite rotation
sprite batching
higher-level texture caching/lifetime policy
DDS decoding
broader TGA variants
map/UI/role/effect/animation-system integration
```

## Intentional Modernization

OpenConquer preserves compatibility-sensitive output and behavior, not legacy implementation
machinery.

```text
preserve logical resolutions
replace D3D8 half-pixel workaround with correct OpenGL pixel-edge mapping
preserve source-region behavior without Win32 RECT*
replace sentinel draw arguments with explicit operations
preserve sprite color behavior without packed DWORD or mutable renderer state
```

Modernization must not change logical coordinates, asset interpretation, framebuffer precision,
blending, timing, protocol-visible behavior, or other verified game behavior.
