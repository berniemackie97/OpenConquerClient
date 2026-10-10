# Native Graphics Compatibility

Verified Conquer Online 5517 graphics behavior implemented by OpenConquer Client. Detailed
reverse-engineering addresses, bounded native replays, and decompilation evidence remain outside
this document.

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

The repeated-source path is restricted to verified compatibility consumers. Ordinary sprite
validation remains bounded.

Verified HUD gauge source geometry is not normalized merely because a hypothetical generalized
input could produce an out-of-range source edge. Native compatibility permits source coordinates
outside the physical image and relies on point sampling plus wrap behavior.

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

## Solid Rectangle Rendering

The native HUD experience bar requires non-textured solid rectangles.

The rendering path preserves:

```text
top-left logical coordinates
integer width / height
RGBA color
alpha blending
no depth test
no depth write
no culling
```

A zero width or zero height produces no visible covered pixels.

## Main HUD

Verified major draw order:

```text
background
vitals
panels
skill / XP
quickbar
action buttons
check controls
selected-skill image / previously armed cover
selected-skill cooldown text
```

Current implementation covers all of those boundaries.

For logical height `H`:

```text
originY = H - 141
```

HUD coordinates are not resolution-scaled.

Supported native HUD logical sizes are:

```text
800×600
1024×768
```

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

A positive value may truncate to destination height 0. Native behavior interprets zero destination
height as natural texture height.

OpenConquer preserves this only inside the HUD-gauge compatibility path.

Zero value remains no draw.

### Native Source Geometry Boundary

The style-0 follower calculation deliberately keeps source rounding and destination rounding
separate.

```text
source pixels      = truncate(followerExact)
destination pixels = round(followerExact)
```

The native path does not clamp the calculated source rectangle to the physical texture before
rendering.

Current verified 5517 HUD consumers remain inside their required operating domain. Do not introduce
a generic source-height clamp merely to normalize hypothetical inputs; that would alter verified
native geometry and repeated-sampling behavior.

### Missing Content

A missing section or declared frame disables only that gauge.

Malformed content is rejected:

```text
unexpected frame count
invalid DDS
unexpected dimensions
```

## Main HUD Skill and Experience

GFX-UI-003 reconstructs the native `Progress42` skill gauge and English-style experience bar.

### Skill Assets

`Control.ani`:

```text
[Progress42]
FrameAmount=3
Frame0=data/main/ProgressPower.dds
Frame1=data/main/ProgressPower.dds
Frame2=data/main/ProgressPowerH.dds
```

Verified identities:

| Asset | Size | SHA-256 | Provenance |
| --- | ---: | --- | --- |
| `ProgressPower.dds` | 128×128 | `c32b0c502b7ab00aff2308a5708ac777fe6ee29ff101bfad056bd4c54ad94248` | `data.wdf` |
| `ProgressPowerH.dds` | 128×128 | `e4d10a40d7ead3d91a3b29cf5339baca487df1f41a40c8afa8c892c80f351868` | `data.wdf` |

Frame 1 intentionally aliases the same physical `ProgressPower.dds` bytes as frame 0.

All three ANI declarations are required. Runtime rendering needs only the two unique textures.

### Layout

```text
skill X      = 0
skill Y      = originY + 50
skill width  = 92
skill height = 92

experience X      = 99
experience Y      = originY + 96
experience width  = 398
experience height = 4
```

Therefore:

```text
800×600  → skill Y 509, experience Y 555
1024×768 → skill Y 677, experience Y 723
```

### Skill State

Normal skill rendering uses the same style-0 gauge geometry as the verified native HUD gauges.

Subvariant behavior:

```text
0 → normal Progress42 fill
1 → alternate Progress42 fill using frame 2
2 → full frame-2 highlight sprite
```

The full highlight draws the complete 128×128 `ProgressPowerH.dds` sprite at the skill origin.

### Skill Highlight Timer

The native highlight timer is intentionally not rewritten as conventional elapsed-time logic.

Arming:

```text
highlight active = true
start timestamp  = 0
current subvariant is preserved until the post-HUD tail
```

First post-HUD advancement while armed:

```text
subvariant = 2
start       = current DWORD tick
```

Subsequent expiration:

```text
deadline = unchecked(start + 500)
expire when now >= deadline
```

The zero timestamp remains a native sentinel.

This has observable DWORD-wrap behavior:

```text
a wrapped deadline can expire early before the clock wraps
the wrapped boundary can expire after wrap
a missed pre-wrap deadline can remain active after wrap
```

Those behaviors are verified compatibility semantics. Replacing this with:

```text
unchecked(now - start) >= 500
```

would be an intentional native-behavior deviation and must not be introduced as a generic timer
cleanup.

### Experience Scaling

Native experience values are 64-bit before reduction to the signed 32-bit style-10 drawing path.

The scale shift is selected from the maximum:

```text
maximum bits 48..63 nonzero → shift 32
maximum high DWORD nonzero  → shift 16
otherwise                   → shift 0
```

The same shift is applied to current experience.

The shifted values are interpreted with native signed 32-bit semantics.

### Positive Experience

If the signed maximum is non-positive, no experience bar is drawn.

For positive values:

```text
value = min(value, maximum)
pixels = truncate(value * 398 / maximum)
```

Positive experience uses three bands:

| Band | Y offset | Height | RGBA |
| --- | ---: | ---: | --- |
| top | 0 | 1 | `F1 D0 6E FF` |
| middle | 1 | 2 | `E8 A3 26 FF` |
| bottom | 3 | 1 | `AB 91 6C FF` |

A positive value that truncates to zero pixels remains a zero-width draw.

### Negative Experience

The native style-10 path performs only an upper clamp:

```text
clampedValue = min(value, maximum)
```

It does not lower-clamp a negative signed value.

Negative values render one red band:

```text
RGBA   = FF 00 00 FF
height = 4
width  = -truncate(value * 398 / maximum)
```

This means sufficiently negative synthetic values can produce a rectangle wider than the nominal
398-pixel experience region.

That behavior is evidence-backed and must not be replaced with a symmetric
`[-maximum, maximum]` clamp without recording an intentional compatibility deviation.

## Main HUD Action Strip

GFX-UI-004 reconstructs the ten native `CMyButton` controls drawn by `CDlgMain`.

GFX-UI-005 reconstructs the four neighboring native `CMyCheck` controls documented separately
below.

### Native Controls and Draw Order

All controls use a 46×22 logical hit rectangle and 64×32 natural-size artwork.

| Draw | Native section | Control ID | Local X | Local Y | Frames |
| ---: | --- | ---: | ---: | ---: | ---: |
| 1 | `Button40` | `0x5DF` | 502 | 94 | 2 |
| 2 | `Button410` | `0x3EE` | 702 | 119 | 2 |
| 3 | `Button42` | `0x3EF` | 552 | 94 | 2 |
| 4 | `Button43` | `0x3F0` | 652 | 119 | 2 |
| 5 | `Main3_MissionBtn` | `0x3F1` | 502 | 119 | 3 |
| 6 | `Button45` | `0x3F2` | 552 | 119 | 2 |
| 7 | `Button46` | `0x3F3` | 602 | 119 | 2 |
| 8 | `Button47` | `0x3F5` | 652 | 94 | 2 |
| 9 | `Main3_OrganiseBtn` | `0x400` | 702 | 94 | 4 |
| 10 | `Button41` | `0x402` | 602 | 94 | 3 |

Final top-row Y positions:

```text
800×600  → 553
1024×768 → 721
```

Final bottom-row Y positions:

```text
800×600  → 578
1024×768 → 746
```

Hit testing uses:

```text
left/top     inclusive
right/bottom exclusive
```

Artwork opacity outside the 46×22 native logical control rectangle does not enlarge the hit region.

### Action Assets

All action-strip DDS frames are verified 64×32 single-level DXT3 images.

| ANI section | Asset | SHA-256 | Retail provenance |
| --- | --- | --- | --- |
| `Button40` | `QueryBtn.dds` | `53bf14f30d91a6771e2b3545444461b9597cd491960677b9b9178844c647f7ef` | package |
| `Button40` | `QueryBtnClick.dds` | `b89ebf6d68b9f28350d10996a3d4d9af1204a02404498f2e4d035b165a4ca475` | package |
| `Button410` | `LevWordBtn.dds` | `43b1f295d5a5c6ce459aee2ba9a9e1eaad0b99f95ff657b3d167e33cd6ec6426` | package |
| `Button410` | `LevWordBtnClick.dds` | `43c7a289ba014b9a839eae158e02bf60c1f7e14f0cea713c6e87c08bb9ebe35d` | package |
| `Button42` | `GoodBtn.dds` | `40f86b049069750334dba62bf186ba8436e5f94223dd436bdd4ac5960c37991f` | package |
| `Button42` | `GoodBtnClick.dds` | `5b79b1afec4a75c2b49529fc43e4bab06d7c284bbd2f0f87e0a3d985a8e1888e` | package |
| `Button43` | `SetBtn.dds` | `f6d62cbfa76a1e3050ff5a4fe1538449d2e78da4fe2fa5250f463da3e7129639` | package |
| `Button43` | `SetBtnClick.dds` | `00bbafdab160f15cd0bea5ae180031fc8d672bf8e43ed0c3a4cfa52d5ad8fc75` | package |
| `Main3_MissionBtn` | `MissionBtnNormal.dds` | `e22897c47610ab53c8a40fb8b8fc2d096a754deedf264f4694a47bb17dc75e6e` | loose |
| `Main3_MissionBtn` | `MissionBtnClick.dds` | `8b5d4fea6621ea46711018bf33ad60b346340cb181108c388922283b75e6cc29` | loose |
| `Main3_MissionBtn` | `MissionBtnEmboss.dds` | `152b11d5239b22b4992d85e8b4526c478f4b72c34bba277d52b8613764fe9eb0` | loose |
| `Button45` | `ChatBtn.dds` | `a4f63e5d6c3041d0b2b80031aa6cf36dc052ce65549ba9958b1f91927f932517` | package |
| `Button45` | `ChatBtnClick.dds` | `0ee1172fb56963a3fdd77beddba91d32d73645dbfe8f516ebd4c0f2697de89ef` | package |
| `Button46` | `GroupBtn.dds` | `5e2a9a8d60daf06c6a3f12b295611b0a12433b92481d4acecfe92d3fc5e438f3` | package |
| `Button46` | `GroupBtnClick.dds` | `b9749ddbcf025e8777a176e70dce507124e7d43bfda9bb77eb9902c72d77c109` | package |
| `Button47` | `PkFree.dds` | `daa715ebbd96119a8977ff82e0fde8abaac87633dac55f340231f6b33b994a08` | package |
| `Button47` | `PkFreeClick.dds` | `651f6acd3db42f07278608752737d6b713d58e0a8e496d6b263c74d8b70e6e4b` | package |
| `Button49` | `PkSafe.dds` | `b4a08b57138750cb2df7f654c039994dde3e6889597303a206dac6ca0d388bbd` | package |
| `Button49` | `PkSafeClick.dds` | `3c0bbf942e62d6e0aef98a6ef3a1284e39eaff309ebcc3abea7479d4dd053969` | package |
| `Button48` | `PkGroup.dds` | `779db2abffe0803ddc4e0ad9373c7de59ef565643a2daf89fff4f92d949d3a8e` | package |
| `Button48` | `PkGroupClick.dds` | `f1a71c88d3cda9dd74c141467b269bf3af1e20d7ff246b91daaae8d160a310c0` | package |
| `Button412` | `PkArre.dds` | `b76d5cb6f912557c53f5400b4c2521c5299a05adcd5ddb1e3d4356aa41b4b37f` | package |
| `Button412` | `PkArreClick.dds` | `374842c513a49a198ee9e67f7bb1543ad541b19eb8e249ed0bf769e9385c5224` | package |
| `Main3_OrganiseBtn` | `OrganiseBtnNormal.dds` | `0b4f52919c0506bf86881e4201859459128df5736552189737751118aa66b360` | loose |
| `Main3_OrganiseBtn` | `OrganiseBtnClick.dds` | `d6291032e2fc79aa6ca3a1715f32c2f3e73dc402b9fb927c9100dc7cae98b03a` | loose |
| `Main3_OrganiseBtn` | `OrganiseBtnUnClick.dds` | `f9382f214d194c56e320933d69d00a637621b015b757401e29e260dbc818aaa3` | loose |
| `Main3_OrganiseBtn` | `OrganiseBtnEmboss.dds` | `276d5d92563a4ae97825a0a2dafb7910788e2902c87ca7a96af2b2b7acdc2e16` | loose |
| `Button41` | `SkillBtn.dds` | `db5a32f59014e65403402003f403c76bcce7e934c8442f7f760d48279a1e0f4a` | package |
| `Button41` | `SkillBtnClick.dds` | `5a2f7810b5a99b8a5c0a8ee01c8da2a7bfc167b511bf90f782cbe220f4cb361a` | package |
| `Button41` | `SkillBtnL.dds` | `d04c0e8c75a3809ac7c27046a818fa2d0510cd8d414cc2a0e5590911f1a5d19a` | package |

Production content resolution remains `LooseThenPackage`.

Conformance independently enforces the exact known retail loose/package provenance above.

### Shared CMyButton State

Logical frame meanings:

```text
0 normal
1 pressed
2 disabled
3 hover
```

All ten controls begin enabled.

Verified state transitions:

```text
WM_ENABLE(FALSE) → frame 2
WM_ENABLE(TRUE)  → frame 0

left down inside enabled control
→ frame 1
→ capture

left up while captured
→ activate only when enabled and release point is inside
→ release capture
→ reset frame 1 to frame 0
```

Mouse-up resets the visual frame only when the current frame is still frame 1.

If an external native state machine overwrites the frame while the button is captured, release
preserves that overwritten frame.

Activation therefore does not depend on `currentFrame == 1`.

None of the ten controls enables the optional hover-frame behavior.

### ANI Frame Modulo

The native ANI renderer wraps a logical frame through the physical section frame count.

Examples:

```text
2-frame control:
logical 0 → physical 0
logical 1 → physical 1
logical 2 → physical 0
logical 3 → physical 1

3-frame control:
logical 3 → physical 0

4-frame Organise control:
logical 0..3 → physical 0..3
```

OpenConquer stores the logical CMyButton frame and applies modulo only at frame selection.

### Pointer Capture

Only one action-strip button may own pointer capture at a time.

While captured:

```text
pointer movement is routed to the capture owner
release outside cancels activation
release inside activates
unmappable framebuffer release still clears capture
```

An unavailable control cannot start interaction.

If a control becomes unavailable while captured, release still clears capture but activation is
suppressed.

### PK Skin State

PK mode selects the visual section:

```text
mode 0 → Button47  / PkFree
mode 1 → Button49  / PkSafe
mode 2 → Button48  / PkGroup
mode 3 → Button412 / PkArre
```

Unknown mode values preserve the current skin.

Skin changes preserve the button's current logical frame and existing blink epoch.

Mode 0 arms the local PK blink gate.

Modes 1, 2, and 3 do not clear an already-active blink gate, so an existing blink may continue on
the newly selected skin.

Repeated mode 0 while already active does not restart the established blink epoch.

### PK Blink State

The PK blink uses a 500 ms phase and a 30,000 ms retention boundary.

When the local gate is active:

```text
initialize process-style epoch once
if start == 0, sample and store again
sample phase clock separately
frame = (unsigned_elapsed / 500) & 1
sample expiry clock separately
```

Expiration:

```text
elapsed <= 30000 → retain gate
elapsed > 30000  → clear gate, frame 0, start 0
```

Exactly 30,000 ms remains active.

30,001 ms expires.

Unsigned DWORD subtraction is intentional.

The initialization flag remains set after expiration.

PK blinking may overwrite a pressed or disabled logical frame before rendering.

### Organise Blink State

Organise uses a separate local gate and process-style epoch.

When active:

```text
initialize epoch once
sample phase clock
frame = 1 + ((unsigned_elapsed / 500) & 1)
```

Therefore Organise alternates physical logical frames:

```text
1 ↔ 2
```

There is no timeout.

There is no zero-start restart rule.

Reset:

```text
clear local gate
set frame 0
preserve initialized global epoch
```

Rearming after reset therefore resumes from the existing epoch rather than restarting the phase.

### Activation Boundary

GFX-UI-004 reconstructs the verified native control boundary and returns the activated control
identity.

It deliberately does not recursively implement the downstream dialog, gameplay, audio, or network
behavior of each action.

Artwork names are not used to invent downstream semantics.

## Main HUD Check Controls

GFX-UI-005 reconstructs the four native `CMyCheck` controls drawn by `CDlgMain` immediately after
the ten GFX-UI-004 `CMyButton` controls.

### Native Controls and Draw Order

| Draw | ANI section | Control ID | Local X | Local Y | Final 800×600 | Final 1024×768 |
| ---: | --- | ---: | ---: | ---: | --- | --- |
| 1 | `Check40` | `0x3F4` | 0 | 23 | `(0,482)` | `(0,650)` |
| 2 | `Check43` | `0x3F7` | 72 | 23 | `(72,482)` | `(72,650)` |
| 3 | `Check46` | `0x3FF` | 50 | 11 | `(50,470)` | `(50,638)` |
| 4 | `Button411` | `0x3F8` | 22 | 11 | `(22,470)` | `(22,638)` |

The parent HUD origin is:

```text
Y = logicalHeight - 141
```

There is no screen-width multiplier.

Each control has a 22×22 logical HWND-equivalent hit rectangle.

Hit testing uses:

```text
left/top     inclusive
right/bottom exclusive
```

The ANI artwork is 32×32 and is rendered at natural size. Artwork outside the 22×22 logical control
rectangle does not enlarge the interactive region.

### ANI State Frames

| Control | State 0 | State 1 |
| --- | --- | --- |
| `Check40` | `RunChk1.dds` | `RunChk2.dds` |
| `Check43` | `MapChk2.dds` | `MapChk1.dds` |
| `Check46` | `ScreenMoveChk1.dds` | `ScreenMoveChk2.dds` |
| `Button411` | `NpcEquip.dds` | `NpcEquipClick.dds` |

All eight frames are verified 32×32 single-level DXT3 DDS images.

Verified encoded SHA-256:

| Asset | SHA-256 |
| --- | --- |
| `RunChk1.dds` | `d4e5ee9cfc3afb45f803bcb589f21a6b10ec65d8295e2554171e3e40dd8dcf58` |
| `RunChk2.dds` | `70b01df50ab75f0cc3f7ecc8958844171924b80d7993e7c81c22a0ca134b03cb` |
| `MapChk1.dds` | `f11580826bad44a032f67a622e728448957dd648588b005c0afebb90627a835f` |
| `MapChk2.dds` | `1662cc91c12d3c8b7fdf193c7855374f84b6fa9fd522e778e38c8fd6fa721e2e` |
| `ScreenMoveChk1.dds` | `a9a6fa6e7ab47211f52074b524a2f4044a16cc0c07dd05867755b8384b62911e` |
| `ScreenMoveChk2.dds` | `6f06ef1bbf0a1f4a0cdb0b759fea78289bc55773897215e71d460f6c99ca927f` |
| `NpcEquip.dds` | `ce4605c39ad53462db6d62ee16d26d9481e50eacf85513d6cbbae61abe3fc45c` |
| `NpcEquipClick.dds` | `0c2b8b6f9e2fc330a056a36b1021d7f67866bae5cee6066df741d92ad1ce6b80` |

Production import resolves these frame requirements with `LooseThenPackage`.

Conformance does not guess a fixed package-vs-loose origin for these frames. Instead each verified
retail frame must resolve from exactly one source in the audited retail root, after which its encoded
SHA-256 is checked.

### Shared CMyCheck State

All four controls initialize to state 0.

The native state byte and render frame advance together.

For the two-state controls:

```text
left-button down
state 0 → state 1
state 1 → state 0
```

The transition occurs on `WM_LBUTTONDOWN`, not mouse-up.

The state object therefore does not use the GFX-UI-004 CMyButton capture/release state machine and
does not perform a mouse-up visual rollback.

The native setter truncates its input to the low byte before checking the state-count bound:

```text
requested = low byte of input

requested < 2
→ update state and render frame

requested >= 2
→ preserve current state and frame
```

The setter itself does not notify the parent.

### Native Parent Handlers

Verified parent mappings are:

```text
Check40   0x3F4 → empty parent handler
Check43   0x3F7 → radar visibility transition
Check46   0x3FF → shell screen-shift state
Button411 0x3F8 → equipment-inspection target mode
```

GFX-UI-005 implements the verified reusable control boundary and local state/rendering behavior.

It deliberately does not recursively implement those downstream parent feature effects.

The exact USER32 capture/release and `BN_CLICKED` behavior for an outside release after native
`CMyCheck` mouse-move handling remains unresolved. That boundary is not required for the proven
mouse-down state transition and is not fabricated by the managed implementation.

## Main HUD Quickbar

GFX-UI-006 reconstructs the native ten-slot main-HUD quickbar/grid.

### Geometry

```text
control ID         0x3FD
native context     3
rows               1
columns            10
local X            90
local Y            98
visual cell        40×40
horizontal stride  41
logical width      410
logical height     40
```

The parent HUD origin remains:

```text
Y = logicalHeight - 141
```

Slot input bounds use the 41×40 stride region. Visual content occupies the verified 40×40 cell.

### Content Kinds

The implemented native content-kind boundary includes:

```text
1  item
2  action
3  magic
4  XP magic
5  dance
6  weapon swap
```

GFX-UI-006 preserves the verified distinction between fixed control artwork and parametric ANI
families. Runtime dependencies are resolved from:

```text
ani/Control.ani
ani/Magic.ani
ani/ItemMinIcon.Ani
ani/effect.ani
```

Implemented rendering/state behavior includes:

```text
slot content
item quantities
upgrade markers
covers
cooldown state
animated glow families
weapon-swap controls
hover state
activation
pickup state
```

The Client owns the consumer-facing quickbar state. Live Gameplay population and downstream
activation side effects remain deferred.

## Main HUD Selected Skill

GFX-UI-007 reconstructs the selected-skill `CMyImage` boundary and its cooldown text consumer.

### Image Geometry

```text
control ID          0x3FE
native image kind   3
native context      3
local X             753
local Y             96
destination         47×46

initial section     Magic0
selected source     (0,0,50,50)

cover section       Image0
cover source        (0,0,64,64)
```

The selected image is resolved through `ani/Magic.ani`. The cover is resolved through
`ani/Control.ani`.

`SetSectionAndContent` activates the image and updates native content/blocking state before ANI
lookup. A failed later lookup therefore does not roll those state mutations back.

`ClearLoadedImage` clears only:

```text
image active
content ID
blocked-cover state
```

It preserves the selected ANI section and independent cover flag.

### Cooldown Text

`ini/info.ini` section `[SelectMagicNum]` supplies:

```text
OffsetX
OffsetY
FontSize
Color
```

Clean/default values are:

```text
OffsetX   = 0
OffsetY   = 0
FontSize  = 20
Color     = 0xFFFFFFFF
```

The cooldown text uses the production native-text façade and the GUI font settings described in
`native-text.md`.

Displayed seconds preserve the verified unsigned ceiling conversion:

```text
0 ms       → no text
1..1000    → 1
1001..2000 → 2
...
```

Equivalent managed expression:

```text
remaining == 0 ? 0 : ((remaining - 1) / 1000) + 1
```

### Cover Ordering

The observable native ordering is stateful:

```text
selected-image Show
    ↓
draw selected image
draw cover if cover flag was already armed
    ↓
cooldown routine
    ↓
clear cover flag
draw cooldown number when active
re-arm cover flag when cooldown continues
```

Therefore a continuing cooldown does not behave like a stateless `icon → text → cover` composition.
The cover used by the current image draw comes from the state armed by the previous cooldown pass.

The managed implementation preserves that ordering by drawing the selected image first and running
the cooldown renderer afterward.

The selected-skill identity and cooldown time are separate Client-owned state seams. Live Gameplay
producers remain deferred.

### GFX-UI-006 / GFX-UI-007 Conformance

Real-driver conformance covers both supported logical resolutions and verifies:

```text
quickbar production rendering against independent reference composition
selected-skill image decode and geometry
selected-skill cover decode and geometry
cleared selected-skill state
selected-only state
previously armed cover state
first cooldown frame
continuing cooldown frame
expired cooldown state
post-draw cover-flag transition
production text-context GPU path
```

The selected-skill retail DDS inputs are independently decoded and compared against production decode
before framebuffer comparison.

## Missing Content Behavior

For HUD ANI-backed controls:

```text
missing Control.Ani
→ consumer unavailable

missing section
→ that consumer unavailable

missing declared frame
→ that consumer unavailable
```

Malformed compatibility content is rejected:

```text
unexpected ANI frame count
invalid DDS
unexpected decoded dimensions
```

One missing action-button, check-control, quickbar, or selected-skill section does not disable unrelated consumers outside that dependency boundary.

## Real-Driver Conformance

Verified environment:

```text
OpenGL   4.1 Metal - 90.5
GLSL     4.10
Vendor   Apple
Renderer Apple M4
Target   RGB565
```

The complete current rendering conformance suite passes against the authorized clean retail 5517
content root.

It verifies both supported logical resolutions:

```text
800×600
1024×768
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

### HUD Skill and Experience

Real-driver conformance independently verifies at both logical sizes:

```text
normal Progress42 fill
alternate Progress42 fill
full highlight frame
positive zero-pixel skill fill

positive experience
positive zero-pixel experience
negative red experience path
64-bit experience shift behavior
```

The production DXT3 decode is compared against an independent reference decoder before rendering.

### HUD Action Strip

Real-driver conformance independently verifies at both logical sizes:

```text
all ten native controls
logical frames 0..3 for every control
ANI modulo behavior
all four PK skins
native draw order
natural 64×32 sprite size
exact retail asset hashes
exact loose/package provenance
production DXT3 decode vs independent reference decode
exact production framebuffer vs independent reference framebuffer
```

The action-strip suite covers 52 logical frame/skin cases per supported resolution.

### HUD Check Controls

Real-driver conformance independently verifies at both logical sizes:

```text
all four native CMyCheck controls
state 0 composition
state 1 for each individual control
native Check40 → Check43 → Check46 → Button411 draw order
verified final geometry
natural 32×32 sprite size
exact retail asset hashes
single-source retail resolution
production DXT3 decode vs independent reference decode
exact production framebuffer vs independent reference framebuffer
```

Verified Apple M4 / OpenGL 4.1 Metal framebuffer hashes:

| Case | 800×600 | 1024×768 |
| --- | --- | --- |
| all state 0 | `e7b429c0728e4dbba90163b87b8b88fb5e4df2b4cd3067f41d95bb4b49cf7eff` | `b7e43a9f94a46a9074decbc33cdfe2c32b8aba30f9a18373782520fd33c79391` |
| Check40 state 1 | `edcea1b6e8d556c065809630ddd434bb84cd30d11a4ac6d832987ec79c916547` | `cec1baa25100e329871e37c59881f00420d8618709b5b2b035b2dbe3a79a9413` |
| Check43 state 1 | `e46124f0610d1e71fd35def2ce72caa645af1b70965cbdcf28cd4fbc36043a03` | `04b6576e734e36c7bcdba5292c3a83c63fa8f90e40e7d0b2fc82bac840976b6f` |
| Check46 state 1 | `ca89ff3de0649132f5c486777e78d8fa16013071c3eeecf387507ae1372be748` | `68474c0463781a65e979332cb0253cde257faad1eb0696ef37cd8a58741a872d` |
| Button411 state 1 | `283600632ef49fbe68654fefcfe213030173378c9f332bb4b08a275f269aa97e` | `853a7d7677750583aa27dee5694935e3accd3666bbe7b0a18c71f7d7e6aae7fe` |

Portable conformance requires production output to equal the independently specified reference.

Driver hashes are evidence for the tested hardware/driver combination, not portable goldens.

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
solid rectangles
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

Progress42 skill
English-style experience bar
skill highlight state/timing

10-slot quickbar/grid
quickbar item/action/magic/XP-magic/dance/weapon-swap content
quickbar hover / activation / pickup state
quickbar quantity / upgrade / cover / cooldown / glow rendering

10-button CMyButton action strip
native action-button hit regions
pointer capture/release behavior
ANI logical-frame modulo
four PK skins
PK blink state
Organise blink state

four main-HUD CMyCheck controls
22×22 native check-control hit regions
32×32 natural-size check-control artwork
two-state mouse-down toggling
native check-control draw order

Magic0 selected-skill image
Image0 selected-skill cover
selected-skill native state boundary
selected-skill cooldown text
stateful cooldown/cover frame ordering

category-8 status-hint backdrop and normal-font text
StrRes.ini localized string resources
CDlgMain-local hotspot rectangles and binary regions
shell-relative status-hint anchors
native MP startup suppression

category-9 learned-magic and zero-magic hints
retail magic/effect/subprofession and configured keyed-localization data
native encoded-byte wrapping, line coloring, and 12-pixel magic font
runtime-snapshot-based category-9 refresh
category-9 real-driver conformance at 800×600 and 1024×768

background → vitals → panels → skill/XP → quickbar → action buttons → check controls → selected skill → cooldown text → category-8 status hints → category-9 Magic hints ordering
real-driver HUD conformance
```

Deferred:

```text
logical-target multisampling
general ANI runtime progression
sprite batching
higher-level texture caching

live hero-state producer
live quickbar producer
live selected-skill / cooldown producer

other status-hint categories / per-control tooltips
outer HUD gate
downstream action-button dialogs and side effects
downstream check-control feature effects

map rendering
role rendering
effect rendering
animation integration
```
