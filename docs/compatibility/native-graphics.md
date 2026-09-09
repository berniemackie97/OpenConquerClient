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

OpenConquer preserves the logical size only:

```text
0 or 1 → 800×600
2 or 3 → 1024×768
```

Desktop window size and mode are modern host policy and do not change logical game coordinates.

## Logical Render Target

### Color

Retail probes:

1. `D3DFMT_R5G6B5`
2. `D3DFMT_X1R5G5B5`

OpenGL mirrors that preference:

1. `RGB565`
2. `RGB5`

The allocated target is accepted only when the driver reports the expected component precision:

```text
RGB565 → R5 G6 B5 A0
RGB5   → R5 G5 B5 A0
```

Dithering is disabled because retail uses `D3DRS_DITHERENABLE = FALSE`.

Retail also contains a later `D3DFMT_X8R8G8B8` fallback after Direct3D device-creation failure.
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
BackBufferCount = 1
SwapEffect = DISCARD
Windowed = TRUE
Depth = D16
PresentationInterval = 0
```

Retail uses a windowed Direct3D device for all four screen modes. Modes 1 and 3 alter desktop/window
state rather than using a fullscreen D3D swap chain.

OpenConquer therefore:

- keeps VSync disabled;
- renders to the fixed logical target;
- presents that target into the physical host framebuffer separately.

### Frame Cadence

Retail gates the outer client frame pipeline at:

```text
25 ms
40 FPS maximum
```

OpenConquer preserves that cadence using monotonic time.

Rules:

- wait only for the remaining interval;
- recheck elapsed time after waiting;
- do not replay missed frames;
- overruns establish the next cadence anchor;
- gameplay, networking, animation, and other clocks remain separate.

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

### Default Sprite State

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

Destination geometry may extend outside the logical target and is clipped normally by the graphics
pipeline.

### Source Regions and Stretching

Retail supports selecting a source region independently from its destination size.

OpenConquer exposes that behavior through a modern source-region type:

```text
SpriteSourceRectangle
├── X
├── Y
├── Width
└── Height
```

Supported source region:

```text
X >= 0
Y >= 0
Width > 0
Height > 0
X + Width <= texture width
Y + Height <= texture height
```

The public rendering operations are:

```text
whole texture → natural size
whole texture → explicit destination size
source region → explicit destination size
```

This preserves observable retail behavior without carrying forward Win32 `RECT*`, null-pointer, or
zero-dimension sentinel APIs.

Source UVs are calculated from pixel coordinates:

```text
u0 = X / textureWidth
v0 = Y / textureHeight
u1 = (X + Width) / textureWidth
v1 = (Y + Height) / textureHeight
```

Destination width and height are independent from the selected source size and must be positive.

No verified 5517 caller currently requires negative, reversed, empty, or out-of-texture source
regions.

## Sprite Conformance

Real-driver conformance uses a 32×32 opaque-black RGB565/RGB555 logical target.

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

These were derived independently from the production decoder.

Historical RGBA8 oracle:

```text
fe8e6998cad4c6399059d43ec465837f53a346878f8d69e200bc7005a348a459
```

It is retained as evidence only; production renders into the 16-bit logical target.

### Whole-Texture Stretch

```text
source:      full 14×14 texture
destination: 20×18
position:    (2, 2)
```

### Source-Region Stretch

```text
source:      (1, 1), 12×12
destination: 20×16
position:    (6, 8)
```

The stretch cases use different non-integer X/Y scale ratios.

Conformance independently derives nearest-neighbor source selection from each destination pixel
center and compares the complete GPU readback byte-for-byte with the expected framebuffer.

The crop/stretch fixture must also remain distinct from the whole-texture stretch fixture.

Verified Apple M4 RGB565 observations:

```text
whole-texture stretch:
45098e61451897bda7b976fc8d55b749ac5d327c1739e9e5fcbc249848b37340

source-region stretch:
f4d724a2e3703eec53fa10df55f64da31c59b706db5db41a00bd8592eb9cbbfd
```

Verified driver:

```text
OpenGL:  4.1 Metal - 90.5
GLSL:    4.10
Vendor:  Apple
Renderer: Apple M4
Target:  RGB565
```

## Host Framebuffer

The physical framebuffer is presentation-only.

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

- host framebuffer is single-sampled;
- `GL_FRAMEBUFFER_SRGB` is disabled before the blit;
- scissor testing cannot clip the presentation blit;
- zero-sized host framebuffers are valid while minimized;
- host resizing does not change logical coordinates or recreate the logical target.

## Current Scope

Verified:

- 800×600 and 1024×768 logical rendering;
- RGB565 / RGB555-compatible color precision;
- exact D16 depth;
- fixed outer 25 ms frame cadence;
- default sprite blending;
- top-left RGBA textures;
- whole-texture natural-size drawing;
- whole-texture stretching;
- source-region selection and stretching;
- nearest sprite sampling;
- logical-target clipping;
- exact real-driver framebuffer conformance.

Not yet verified or implemented:

- logical-target multisampling;
- ANI runtime frame progression and timing;
- sprite tint/color;
- draw parameters 1 and 2;
- sprite rotation;
- sprite batching;
- higher-level texture caching/lifetime policy;
- DDS decoding;
- broader TGA variants;
- map, UI, role, effect, and animation-system integration.

## Intentional Modernization

OpenConquer preserves compatibility-sensitive output and behavior, not legacy implementation
machinery.

Examples:

- logical resolutions are preserved; retail desktop shell behavior is not;
- D3D8 half-pixel correction is replaced by correct OpenGL pixel-edge mapping;
- source-region behavior is preserved without exposing Win32 `RECT*`;
- legacy sentinel arguments are replaced by explicit rendering operations.

Modernization must not change logical coordinates, asset interpretation, framebuffer precision,
blending, timing, protocol-visible behavior, or other verified game behavior.
