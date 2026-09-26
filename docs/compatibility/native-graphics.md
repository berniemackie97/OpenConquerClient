# Native Graphics Compatibility

Verified Conquer Online 5517 graphics behavior implemented by OpenConquer Client. Detailed reverse-engineering evidence remains outside this document.

## Logical Frame

Retail `ini/GameSetUp.ini` maps screen modes to:

| Modes | Logical size |
| --- | ---: |
| 0, 1 | 800×600 |
| 2, 3 | 1024×768 |

Logical rendering uses:

```text
RGB565 or RGB555-compatible color
D16 depth
opaque black clear
depth clear = 1.0
```

Desktop size and presentation scaling do not change logical coordinates.

## Presentation

```text
logical target
→ presentation transform
→ host framebuffer
```

Verified outer cadence is approximately 25 ms / 40 FPS.

```text
wait only for remaining frame time
recheck after waiting
do not replay missed frames
overruns establish the next cadence anchor
```

Logical-target multisampling remains unimplemented.

## Sprite Rendering

### Content Boundary

Supported verified ANI image formats:

```text
.tga
.dds / single-level DXT3
```

Images cross into Rendering as top-left RGBA.

GPU textures use:

```text
RGBA8
nearest filtering
single mip level
```

### DDS

Implemented subset:

```text
2D
single level
DXT3 / BC2
explicit 4-bit alpha
RGB565 color endpoints
no cubemaps
no volume textures
no mip chains
no trailing payload
```

### State

Default sprite state:

```text
blend       = alpha
depth test  = disabled
depth write = disabled
culling     = disabled
```

Supported blending:

| Mode | Source | Destination |
| --- | --- | --- |
| Alpha | source alpha | one minus source alpha |
| Additive | one | one |

### Coordinates

OpenGL uses pixel-edge coordinates directly:

```text
xNdc = 2 * x / width - 1
yNdc = 1 - 2 * y / height
```

Coordinates are top-left oriented.

### Source Geometry

Ordinary sprite draws use `SpriteSourceRectangle`:

```text
non-negative coordinates
positive width and height
fully contained within texture
clamp-to-edge
```

Verified compatibility draws use `SpriteSourceBounds`:

```text
non-negative edges
right >= left
bottom >= top
degenerate spans allowed
out-of-range right/bottom allowed
nearest filtering
REPEAT addressing
```

The repeated-source path is restricted to verified compatibility consumers. Ordinary sprite validation remains bounded.

### Color

`SpriteColor` performs RGBA modulation:

```text
sampled texture × normalized color
```

### Rotation

Verified rotation:

```text
signed integer degrees
angle % 360
clockwise in screen coordinates
pivot at destination center
```

## Main HUD

Verified major draw order:

```text
background
vitals
panels
skill / XP
controls / overlays
```

Current implementation covers background, vitals, and panels.

For logical height `H`:

```text
originY = H - 141
```

HUD coordinates are not resolution-scaled.

## Main HUD Chrome

### Assets

`Control.ani`:

```text
[Progress45]
FrameAmount=1
Frame0=data/main/ProgressBk.dds

[Dialog4]
FrameAmount=2
Frame0=data/main/mainDialog1.dds
Frame1=data/main/mainDialog2.dds
```

Verified identities:

| Asset | Size | SHA-256 |
| --- | ---: | --- |
| `Control.Ani` | 317837 bytes | `a1db47baaeb2f75eea3b5216378f97d713c2afeaefe05ec02e6f584794532d27` |
| `ProgressBk.dds` | 256×256 | `9b91a28e0170142a48dc03332691959eca01c179b9d8c592aa5b11af4f5d966c` |
| `mainDialog1.dds` | 256×256 | `505a4655c398e41bd25698b57caa50f48376cff713e1c8c6f287a03866038fe1` |
| `MainDialog2.dds` | 256×128 | `818d13f62509ac859fb0ed72d36ec6aeb2b12f9ed3dee3c6172171d99ba86f41` |

Verified provenance:

```text
Control.Ani     → loose
ProgressBk      → data.wdf
mainDialog1     → data.wdf
mainDialog2     → loose
```

### Layout

```text
Progress45 frame 0
destination (0, originY)
```

```text
A: frame 0, source (0,112,256,144), destination (0, originY-3)
B: frame 0, source (0,0,256,54),    destination (256, originY+88)
C: frame 1, source (0,0,256,54),    destination (512, originY+88)
D: frame 1, source (0,64,256,54),   destination (768, originY+88)
```

Panel D naturally clips at 800×600.

## Main HUD Vitals

### Assets

`Control.ani`:

```text
[Progress40]
FrameAmount=3
Frame0=data/main/ProgressHP.dds
Frame1=data/main/ProgressHPA.dds
Frame2=data/main/ProgressHPH.dds

[Progress41]
FrameAmount=3
Frame0=data/main/ProgressMP.dds
Frame1=data/main/ProgressMPA.dds
Frame2=data/main/ProgressMPH.dds

[Progress46]
FrameAmount=2
Frame0=data/main/ProgressForce.dds
Frame1=data/main/ProgressForceA.dds

[Progress47]
FrameAmount=2
Frame0=data/main/ProgressForce2.dds
Frame1=data/main/ProgressForce2A.dds
```

Verified identities:

| Asset | Size | SHA-256 |
| --- | ---: | --- |
| `ProgressHP.dds` | 128×128 | `ecd40dbdebc5e582860c91deeb28a29b8adaaf9dbc8285e8c5404a5f1ab6be2d` |
| `ProgressHPA.dds` | 128×128 | `2ad8122875e02d25ff53ed281b9214fcbde1689a773fdb59e0bba355bde0c54d` |
| `ProgressHPH.dds` | 128×128 | `f85e3d2287f9f3cda60b3494dcb36359479d738c34030d3266530026080d2679` |
| `ProgressMP.dds` | 128×128 | `01fbd1e55d5266a823ffb2347a96721f8e9b7ec18c4393f18ee4488ccac605be` |
| `ProgressMPA.dds` | 128×128 | `324ddfd751ec47123285a26905d00d56694ffd7461f38f6b3e6e3125095952e1` |
| `ProgressMPH.dds` | 128×128 | `b8dfdad6025fbd28201d92d9fe95f8ff504450209d43feeec57aaa0a30f4ebc6` |
| `ProgressForce.dds` | 128×128 | `f030dbbd0823b08d12901ebf803adee687df015d3d017793f67959db9d9e8192` |
| `ProgressForceA.dds` | 128×128 | `3b7177b3bb0405e3c7f0a68aaaa3e6ca5b054ff63d798887eec5687adc76154b` |
| `ProgressForce2.dds` | 32×32 | `9a67c108613cdf5440a40708bcbd3573ef9b3782da6d997c435b654a7fbbada9` |
| `ProgressForce2a.dds` | 32×32 | `77ebf0be6f6f4621ecfb28f1e04f39f6f95bce647fc0d27af44bcf4b2c13c789` |

Verified provenance:

```text
ProgressHP       → data.wdf
ProgressHPA      → data.wdf
ProgressHPH      → data.wdf
ProgressMP       → data.wdf
ProgressMPA      → data.wdf
ProgressMPH      → data.wdf
ProgressForce    → data.wdf
ProgressForceA   → data.wdf
ProgressForce2   → loose
ProgressForce2A  → loose ProgressForce2a.dds
```

All ten frames are required and validated. Eight verified reachable frames are uploaded to the GPU.

### Layout

| Gauge | X | Y |
| --- | ---: | ---: |
| Life | 4 | `originY + 54` |
| Mana | 52 | `originY + 54` |
| Stamina | 42 | `originY + 58` |
| Extended stamina | 42 | `originY + 52` |

Supported logical resolutions:

```text
800×600
1024×768
```

### Fill Dimensions

| Gauge | Width | Height | Alternate width |
| --- | ---: | ---: | ---: |
| Life | 36 | 74 | 86 |
| Mana | 34 | 74 | 34 |
| Stamina | 8 | 70 | 8 |
| Extended stamina | 8 | 35 | 8 |

Fills grow bottom-up.

```text
pixelsPerUnit = height / range
cappedPixelsPerUnit = range >= 100 ? height * 0.01 : pixelsPerUnit
```

Compatibility calculations use C# `float`.

### Subvariants

Subvariant 0:

```text
current <= follower
→ frame 0

current > follower
→ frame 1 current
→ frame 0 biased follower
```

Subvariant 1:

```text
current <= follower
→ frame 2 current

current > follower
→ frame 2 follower only
```

Life starts at subvariant 1 and permanently switches to 0 after positive clamped mana is observed.

Mana starts at subvariant 0 and exposes the verified alternate-state toggle.

### Stamina

Regular stamina uses `Progress46` frame 0.

Extended stamina renders when:

```text
HasExtendedStaminaGauge
Stamina >= 100
```

Range:

```text
0..50
```

Value:

```text
Stamina - 100
```

### Progress47 Compatibility

`ProgressForce2.dds` is 32×32 while the configured fill height is 35.

Verified behavior:

```text
source bottom = 35
texture height = 32
sampling = point + wrap
```

Examples:

```text
value 25 → source top 18, destination height 17
value 50 → source top 0,  destination height 35
```

A positive value may truncate to destination height 0. Native behavior interprets zero destination height as natural texture height.

OpenConquer preserves this only inside the HUD-gauge compatibility path.

Zero value remains no draw.

### Missing Content

A missing section or declared frame disables only that gauge.

Malformed content is rejected:

```text
unexpected frame count
invalid DDS
unexpected dimensions
```

## Real-Driver Conformance

Verified environment:

```text
OpenGL   4.1 Metal - 90.5
GLSL     4.10
Vendor   Apple
Renderer Apple M4
Target   RGB565
```

### Repeated Sampling

| Case | SHA-256 |
| --- | --- |
| Out-of-range source | `c684f3e8afed2d1748d26c3c81c5000c8234896153c21e6587f3e4242fad0343` |
| Degenerate source | `ea984d551906587c2b0c3d62abf81e05c7103258bba2d97973c401899a0aa46b` |

### HUD Chrome

| Resolution | SHA-256 |
| --- | --- |
| 800×600 | `bf905c1d0fdadf486303ba4849221bec836b8a6f52f6b23a4d91b09244cd8cf1` |
| 1024×768 | `dbdaa4cd332fda6661d438ad5b29b97d051ec2d51cc8149b92a2e4aede4fb629` |

### HUD Vitals

| Case | 800×600 | 1024×768 |
| --- | --- | --- |
| HP normal dual | `d8c78f9d71e330ba0e992102a715a01416a15ccc8e3eb2d9164e0fcd5ce64320` | `541583eada1a2129f172772090a3fb24a6c1261e708b4bb7264acec6b7c4e221` |
| HP startup frame 2 | `ac25430ae00fae6a1664fdbb9ed1ba58cf230e0433dc8784856a2cbf839a9095` | `3919458824463798e757336acce23a0e2e2e8ad26c1b6d88d05217962985d304` |
| MP normal dual | `c097586a338a14814bddeccb692620223419ef52e3ae429bcd84a3e5f04e9c82` | `b0b5eb854cd8de698e7a87a7a0fb079e50dada2d510550f4a43d56665f71f212` |
| MP frame 2 | `a63f35172830231baa873afb8bac91bd0f94a88b2fc5c87aef9ee5712cc19689` | `8ae10d0d8fc43b7995f8474e6942b9398bd6bbad38e4550d3914dadee2a73a73` |
| Stamina | `d669a69119f5ca847bfc04757cf0eb5f4a542e4f47bbc26ea5803a08e0974f63` | `a5832031373f367ece08d2c3a73d8d10277c20975bc54cbf1c40fcd4cc5c4abe` |
| Overflow 25 | `9b55557a33a2be64cd05889fafea48e8b2cfb9010de746b41ca12b4c7923056d` | `b70960f21268d25ad5cc7deca68e0cbdca7818da59be211465d025cb30f05da0` |
| Overflow 50 | `5110a75dc5aed6aa232afe9921ff8072cb200f652713d7f673a9342fd1cec222` | `87b955846e713cb394c9e264bd38bd85e42a0429bd9d4dec097f6393a883b537` |
| Positive zero-pixel | `554f17f77f7eeaa11afdc8c7455bfd3edfc05c5add2d0be4e40f23e3f478158d` | `3f990c179b5f064543233693d6a76377133767f3940d6a9cde0f162b8eb06e0f` |

Portable conformance requires production output to equal the independently specified reference. Driver hashes are evidence, not portable goldens.

## Current Scope

Implemented:

```text
800×600 and 1024×768 logical rendering
RGB565 / RGB555-compatible logical target
D16 depth

TGA
DXT3 DDS
bounded sprites
repeated compatibility sprites
RGBA modulation
alpha blending
additive blending
integer rotation

Progress45 HUD background
Dialog4 HUD panels
Progress40 life
Progress41 mana
Progress46 stamina
Progress47 extended stamina

background → vitals → panels ordering
real-driver HUD conformance
```

Deferred:

```text
logical-target multisampling
ANI runtime progression
sprite batching
higher-level texture caching
live hero-state producer
skill / XP HUD
HUD controls and overlays
outer HUD gate
map, role, effect, and animation integration
```
