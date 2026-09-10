# Native Graphics Compatibility

Compatibility requirements for reconstructing Conquer Online 5517 graphics behavior. Detailed
reverse-engineering evidence belongs in the native analysis notes.

## Logical Resolution

Retail `ini/GameSetUp.ini` maps screen modes to two logical render sizes:

| Modes | Logical size |
| ----- | -----------: |
| 0, 1  |      800×600 |
| 2, 3  |     1024×768 |

Window size, fullscreen mode, and presentation scaling are modern host concerns and do not change
logical game coordinates.

## Logical Render Target

### Color

Retail prefers:

```text
D3DFMT_R5G6B5
D3DFMT_X1R5G5B5
```

OpenConquer uses:

```text
RGB565 → R5 G6 B5
RGB5   → R5 G5 B5
```

Dithering is disabled.

Retail also contains an `X8R8G8B8` fallback tied to Direct3D device-creation failure. OpenConquer
does not treat generic OpenGL failure as equivalent.

### Depth

Verified retail callers use `D3DFMT_D16`.

OpenConquer requires:

```text
16-bit depth
no stencil
```

The dormant retail `D24S8` path is not part of the verified 5517 contract.

### Frame Clear

Each logical frame starts with:

```text
color = opaque black
depth = 1.0
```

## Presentation

Verified retail presentation uses:

```text
BackBufferCount      = 1
SwapEffect           = DISCARD
Windowed             = TRUE
PresentationInterval = 0
```

Retail screen modes alter desktop/window state rather than switching the Direct3D device to a
fullscreen swap chain.

OpenConquer keeps logical rendering separate from physical presentation:

```text
logical target
    ↓
presentation transform
    ↓
desktop framebuffer
```

### Frame Cadence

Retail gates the outer client frame pipeline at approximately:

```text
25 ms
40 FPS maximum
```

OpenConquer preserves:

```text
wait only for remaining frame time
recheck time after waiting
do not replay missed frames
overruns establish the next cadence anchor
gameplay/network/animation clocks remain independent
```

### Multisampling

Retail can select 2×, 4×, or 8× multisampling depending on configuration and format support.

Logical-target multisampling is not implemented yet. The desktop host therefore requests zero
framebuffer samples.

## Sprite Rendering

### Verified Asset

Current framebuffer conformance uses:

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

TGA support remains limited to formats required by verified runtime content.

### Default State

Retail default sprite drawing uses:

```text
blend       = enabled
depth test  = disabled
depth write = disabled
culling     = disabled

source blend      = SRCALPHA
destination blend = INVSRCALPHA
```

OpenGL equivalent:

```text
BlendEquation = Add
BlendFunc      = SrcAlpha, OneMinusSrcAlpha
```

Sprite textures use:

```text
RGBA8
nearest filtering
clamp-to-edge
single mip level
```

### Coordinates

Retail applies a Direct3D 8 `-0.5` screen-space correction. This rasterization workaround is not
carried into OpenGL.

OpenConquer maps logical pixel edges directly:

```text
xNdc =  2 * x / width - 1
yNdc =  1 - 2 * y / height
```

Coordinates are top-left oriented. Geometry outside the logical target is clipped by the graphics
pipeline.

### Source Regions and Stretching

`SpriteSourceRectangle` represents a non-empty texture region:

```text
X >= 0
Y >= 0
Width > 0
Height > 0
region must fit within texture
```

Supported operations:

```text
full texture → natural size
full texture → explicit destination size
source region → explicit destination size
```

Source rectangles affect UVs only. Destination dimensions independently establish sprite geometry.

This replaces native `RECT*`, null-pointer, and zero-dimension sentinel APIs with explicit
operations.

### Color

Retail sprite diffuse color uniformly modulates sampled texture RGBA.

OpenConquer uses:

```text
SpriteColor(Red, Green, Blue, Alpha)
```

Neutral modulation:

```text
SpriteColor.White = (255, 255, 255, 255)
```

Rendering behavior:

```text
sampled texture × normalized sprite color
    ↓
source-alpha blending
```

Color is explicit per draw. Rendering does not preserve packed Direct3D colors or mutable native
sprite-color state.

### Rotation

Verified retail rotation semantics:

```text
input              = signed integer degrees
angle reduction    = angle % 360
positive direction = clockwise in screen coordinates
pivot              = destination center
```

Destination geometry is established before rotation:

```text
source region
    ↓
destination size / stretch
    ↓
rotate about destination center
    ↓
logical-pixel → OpenGL coordinates
```

Rotation preserves:

```text
UVs
color
texture
```

Native `Sprite_Rotate` mutates existing vertices, but all seven recovered rotating callers rebuild
destination geometry before invoking it. OpenConquer therefore uses explicit per-draw rotation
rather than mutable sprite history.

An original angle of `0` uses the unrotated path. Other values use signed modulo reduction.

CPU-specific x87/SSE/3DNow! approximation behavior is native implementation machinery rather than a
portable rendering requirement.

## Sprite Conformance

Real-driver conformance renders through the production OpenGL path and compares logical framebuffer
output against independent expectations.

### Natural Sprite

```text
source:      full 14×14 Syndicate frame
destination: 14×14
position:    (7, 9)
```

Hashes:

```text
RGB565:
93939cf5e51ea6298b729836b80561627550505e6f0771588082c5a33142833c

RGB555:
313ec6083e2eb72c7e3e63594859225c09bee573d155afa9e24d0399fd203ed7
```

### Stretch

```text
source:      full 14×14
destination: 20×18
position:    (2, 2)

RGB565:
45098e61451897bda7b976fc8d55b749ac5d327c1739e9e5fcbc249848b37340
```

### Source Region + Stretch

```text
source:      (1, 1), 12×12
destination: 20×16
position:    (6, 8)

RGB565:
f4d724a2e3703eec53fa10df55f64da31c59b706db5db41a00bd8592eb9cbbfd
```

### Color

Explicit white remains byte-identical to the natural sprite case.

Non-white probe:

```text
texture = opaque white
color   = (255, 128, 64, 128)

RGB565:
3fc3d1e606877135ff6296dd684e01d77756917cf05cbc7a756765f6e50eb1b7
```

This verifies RGB modulation, alpha modulation, blending, and explicit-color sprite operations.

### Rotation

Rotation probe:

```text
texture:       blue | red | green | yellow
source region:       red | green
destination:   4×2 at (2, 2)
angle:         1,440,000,090° → 90°
pivot:         destination center
target:        8×6
```

The large input makes signed `% 360` reduction observable rather than relying on trigonometric
periodicity alone.

Expected clockwise result:

```text
........
...RR...
...RR...
...GG...
...GG...
........
```

This verifies:

```text
signed angle reduction
clockwise direction
destination-center pivot
stretch-before-rotation ordering
source-region UV preservation
production rotating draw path
```

Verified Apple M4 RGB565 hash:

```text
56f3bd55bec37797ec2e6b30f9db8daf8a8347417ee8a09fb86d59cb952e17f7
```

### Verified Driver

```text
OpenGL:   4.1 Metal - 90.5
GLSL:     4.10
Vendor:   Apple
Renderer: Apple M4
Target:   RGB565
```

## Host Framebuffer

The desktop framebuffer is presentation-only:

```text
logical RGB565/RGB5 + D16 target
    ↓
OpenGL framebuffer blit
    ↓
desktop framebuffer
    ↓
platform swap
```

Current requirements:

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

sprite alpha blending
top-left RGBA textures
natural-size drawing
stretching
source regions
nearest sampling
RGBA modulation
integer-degree rotation
logical-target clipping

real-driver framebuffer conformance
```

Remaining:

```text
logical-target multisampling
ANI runtime progression/timing
draw parameters 1 and 2
sprite batching
higher-level texture caching
DDS decoding
additional required TGA variants
map/UI/role/effect/animation integration
```

## Modernization Boundary

Preserve observable 5517 behavior, not obsolete implementation machinery.

```text
preserve logical coordinates and framebuffer precision
replace D3D8 half-pixel correction with correct OpenGL mapping
replace RECT*/sentinel APIs with explicit operations
replace packed/mutable sprite color with per-draw SpriteColor
replace mutable C3Sprite rotation history with verified per-draw rotation
do not emulate CPU-specific native math dispatch without a compatibility requirement
```
