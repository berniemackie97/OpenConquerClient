# Native Graphics Compatibility

This document records verified graphics behavior from the retail Conquer Online 5517 client that
constrains the OpenConquer Client implementation.

It defines compatibility requirements rather than implementation architecture. Detailed
reverse-engineering evidence remains in the native analysis notes.

## Rendering Resolution

Retail 5517 defines four screen modes:

| Mode | Logical resolution | Shell behavior          |
| ---: | -----------------: | ----------------------- |
|    0 |            800×600 | windowed                |
|    1 |            800×600 | display-mode fullscreen |
|    2 |           1024×768 | windowed                |
|    3 |           1024×768 | display-mode fullscreen |

All four modes use a windowed Direct3D 8 device. Modes 1 and 3 change the desktop display mode and
window placement rather than creating a Direct3D fullscreen swap chain.

Retail obtains the selected mode from:

```ini
[ScreenModeRecord]
ScreenMode=<value>
```

in:

```text
ini/GameSetup.Ini
```

OpenConquer Client reads that configuration during startup and preserves the verified logical
resolution mapping:

```text
0 or 1 → 800×600
2 or 3 → 1024×768
```

The selected dimensions become the fixed logical rendering surface.

The modern client intentionally uses a resizable desktop host. Host resizing does not implicitly
change the game's logical rendering resolution.

The display-mode and fixed-shell behavior associated with retail modes 1 and 3 is not currently
reproduced. That remains an intentional desktop-host difference rather than changing the verified
logical resolution associated with those modes.

## Depth and Stencil

Retail graphics initialization selects:

```text
D3DFMT_D16
```

for every reachable 5517 startup and reset path.

The C3 graphics library contains a dormant `D3DFMT_D24S8` branch controlled by bit 31 of the upper
`HintGraphicDetail` flags. The retail client never sets that bit:

- the only retail `HintGraphicDetail` calls pass `0`, `2`, and `0`
- `HintGraphicDetail` is the sole writer of the upper-flags global
- `Init3DEx` is the sole reader
- reset and resolution-change paths preserve the established presentation parameters

The reachable retail renderer also does not use stencil render states or stencil clears.

The OpenGL logical render target therefore uses a 16-bit depth-only attachment and no stencil
attachment.

Because OpenGL may provide greater component precision than the requested minimum for renderbuffer
storage, the renderer queries the allocated depth component size and accepts the logical target only
when OpenGL reports exactly 16 depth bits.

Depth precision is compatibility-sensitive. Retail rendering actively uses depth testing, depth
writes, and depth clears, so replacing D16 with a higher-precision format is not treated as an
implementation-neutral modernization.

The ordinary retail frame clears both the color and depth buffers, using opaque black (`0xFF000000`)
and a depth value of `1.0`.

OpenGL frame initialization mirrors those values and establishes deterministic full-target clear
state by:

- disabling scissor testing
- enabling writes to every color component
- enabling depth writes
- clearing color to opaque black
- clearing depth to `1.0`

## Presentation

Retail `D3DPRESENT_PARAMETERS` are cleared before initialization. The effective presentation
configuration includes:

- `BackBufferCount = 1`
- `SwapEffect = D3DSWAPEFFECT_DISCARD`
- `Windowed = TRUE`
- automatic depth/stencil enabled with `D3DFMT_D16`
- `Flags = 0`
- `FullScreen_RefreshRateInHz = 0`
- `FullScreen_PresentationInterval = 0`

The presentation interval remains unchanged through device reset and resolution changes.

Every retail screen mode uses a windowed Direct3D 8 device with the discard swap effect. The
effective retail presentation path is immediate rather than explicitly synchronized to vertical
retrace.

The modern desktop host therefore does not force VSync.

### Outer Client Frame Cadence

The retail outer client loop uses a 25 ms gate around its frame pipeline.

The effective maximum cadence is therefore:

```text
25 ms per frame
= 40 frames per second
```

When less than 25 ms has elapsed since the previous frame boundary, retail waits for the remaining
interval before allowing the next frame pipeline execution.

OpenConquer Client preserves this as an explicit application policy.

`ClientApplication` owns the verified 25 ms value and supplies it to `DesktopWindow`. Platform owns
the pacing mechanism but does not contain a Conquer-specific frame-rate constant.

The modern pacer follows these compatibility and runtime-safety rules:

- elapsed time comes from a monotonic timestamp source
- early frames wait only for the remaining portion of the 25 ms interval
- elapsed time is rechecked after a wait rather than assuming the requested sleep duration was exact
- an overrun does not incur an additional wait before the next frame
- missed frame intervals are not replayed
- an overrun establishes the actual next frame as the new cadence anchor instead of creating a
  catch-up burst

Silk.NET's independent render and update rate limiters remain uncapped. OpenConquer owns cadence in
its custom outer desktop loop, so adding another Silk.NET limiter would create overlapping pacing
policies.

VSync remains disabled. The explicit outer cadence and the swap interval are separate controls.

The 25 ms outer frame gate is not treated as a universal client clock. Gameplay simulation timing,
startup-host timers, network deadlines, animation timing, and other domains remain separate unless
additional native evidence establishes a shared contract.

Presentation cadence is therefore separate from gameplay simulation timing.

## Multisampling

`HintGraphicDetail` divides its argument into two independent controls:

- bits 0–3 select a multisample quality tier
- bits 4–31 are retained as upper graphics-detail flags

Retail callers use values whose upper bits are clear, so the multisample tier does not alter the
verified D16 depth-format choice.

The native renderer can select a supported multisample type from 8×, 4×, or 2× candidates when the
requested tier enables multisampling. Compatibility checks require both the color and depth formats
to support the selected sample type.

Multisampling configuration has not yet been implemented in OpenConquer Client.

Until the native multisampling policy is implemented for the logical render target, the desktop host
explicitly requests zero framebuffer samples. This prevents backend-default host multisampling from
changing the host-composition path or interfering with scaled framebuffer blits.

## Color Format

Retail 5517 probes the main back-buffer color format in this order:

1. `D3DFMT_R5G6B5`
2. `D3DFMT_X1R5G5B5`

If both format probes fail, graphics initialization fails rather than selecting a different format.

If the selected 16-bit format passes validation but later Direct3D device-creation attempts fail,
the native graphics layer contains a separate `D3DFMT_X8R8G8B8` device-creation fallback.

The distinction matters:

```text
format probe
    ↓
R5G6B5
    ↓ fallback
X1R5G5B5
    ↓
selected 16-bit color format
    ↓
Direct3D device creation
    ↓ failure after behavior retries
X8R8G8B8 device-creation fallback
```

The OpenGL backend preserves the verified 16-bit format preference rather than treating the native
32-bit device-creation fallback as a generic render-target fallback.

The logical render target attempts:

1. `RGB565`, corresponding to retail `R5G6B5`
2. `RGB5`, corresponding to the RGB precision of retail `X1R5G5B5`

`RGB565` is used only when the active OpenGL implementation guarantees support through OpenGL 4.2 or
later, or through `GL_ARB_ES2_compatibility`.

The renderer does not assume that requesting a sized internal format guarantees the exact storage
precision. After texture allocation it queries the actual component sizes reported by OpenGL.

A color target is accepted only when its allocation reports one of these exact component layouts:

```text
RGB565
R = 5
G = 6
B = 5
A = 0

RGB5
R = 5
G = 5
B = 5
A = 0
```

This prevents a driver from silently substituting a higher-precision texture while the client
believes it is preserving retail color quantization.

If neither compatible 16-bit layout can be created, graphics initialization fails explicitly rather
than silently changing the game's color precision.

The native `D3DFMT_X8R8G8B8` fallback does not currently have a direct OpenGL equivalent. Its native
trigger is failure during Direct3D device creation, not rejection of both 16-bit back-buffer format
probes. Mapping an OpenGL render-target failure to that path would introduce behavior not
established by the native evidence.

Retail color precision is compatibility-sensitive. The original renderer performs blending into its
selected back buffer, so changing the target from 16-bit RGB to an 8-bit-per-channel format can
change quantization and blended pixel results.

Retail rendering also establishes `D3DRS_DITHERENABLE = FALSE`. OpenGL enables dithering by default,
and dithering affects conversion into fixed-point framebuffer precision. Logical framebuffer
rendering therefore explicitly disables OpenGL dithering rather than inheriting the OpenGL default.

## Host Framebuffer

The OpenGL host framebuffer is presentation-only from the game's perspective.

Game rendering occurs in the fixed logical render target. Only its color buffer is copied to the
desktop framebuffer, so the host framebuffer does not require its own depth or stencil storage.

The host framebuffer is explicitly requested without multisampling. Multisampling compatibility
belongs to the logical game-rendering path rather than the platform host surface.

Current ownership and flow are:

```text
logical render target
├── RGB565 preferred / RGB5 fallback color texture
└── exact D16 depth renderbuffer
        │
        │ OpenGLRenderer linear color blit
        ▼
desktop framebuffer
├── color
├── single-sampled
├── no requested depth
└── no requested stencil
        │
        │ Platform native buffer swap
        ▼
desktop window
```

`OpenConquer.Rendering` owns the logical-to-host color blit. `OpenConquer.Platform` owns the native
window, OpenGL context, and host-buffer swap.

The logical color attachment is copied across the complete physical framebuffer using a linear
framebuffer blit.

Before the blit, Rendering disables scissor testing so later scene state cannot clip the host
composition operation.

Rendering also disables `GL_FRAMEBUFFER_SRGB` before the blit. The compatibility path does not rely
on implicit framebuffer sRGB conversion, and host composition must not change because unrelated
rendering code leaves that state enabled.

The renderer validates that the host framebuffer is single-sampled before using the current scaled
blit path.

Physical host resizing does not recreate the logical render target or alter its coordinate system.

A zero-width or zero-height physical framebuffer is valid while the host is minimized. Host
composition is skipped in that state without changing or recreating the logical render target.

## Intentional Differences

The modern client intentionally differs from retail in desktop-window behavior.

Retail uses fixed-size shell behavior associated with its four screen modes. OpenConquer Client uses
a resizable host window while retaining the fixed logical rendering surface selected by the retail
screen-mode configuration.

This difference must remain confined to the desktop presentation boundary. Logical game coordinates,
content layout, simulation behavior, and protocol-visible behavior must not become dependent on the
physical host framebuffer size.
