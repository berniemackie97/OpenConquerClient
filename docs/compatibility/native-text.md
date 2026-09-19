# Native Text Compatibility

Compatibility contract for Conquer Online 5517 text configuration, encoded-text semantics, font
resolution, glyph rasterization, caching, atlas storage, layout, render-style behavior, OpenGL
resource ownership, and GPU drawing.

Detailed native addresses, decompilation traces, caller inventories, and unresolved
reverse-engineering evidence belong in the native analysis notes.

## Startup Configuration

### Game Font

Retail `ini/Font.ini` configures the initial game-font request.

Format:

```text
<face token><space><nominal pixel height>
```

Parsing uses the final ASCII space, allowing spaces inside face names.

Clean 5517 content:

```text
Arial 12
```

Contract:

```text
face token            = bytes before final ASCII space
nominal pixel height  = integer suffix
invalid/zero height   = 12
negative nonzero      = preserved
```

The face token is preserved exactly at the Content boundary.

`Font.ini` is a loose-file dependency; WDF packages do not satisfy it.

OpenConquer limits the file to 256 bytes and rejects larger inputs instead of reproducing native
fixed-buffer truncation.

The file is not added to the managed runtime content closure until an implemented runtime consumer
requires it.

### Native Font Tokens

The native text system accepts family names and file-oriented tokens:

```text
$*.tt?
```

The leading `$` selects file-token semantics.

Examples:

```text
$simsun.ttc
$simsun.ttf
```

The `?` represents the third character of the three-character extension; OpenConquer does not treat
it as a wildcard. Accepted extensions have three characters whose first two characters are `tt`.

File tokens resolve only against fonts registered with the host font system. They do not trigger
recursive filesystem searches or arbitrary path access.

### Normal UI Font Height

Retail `ini/info.ini` contains:

```ini
[FontSize]
Size=12
Width=6
```

Current consumers require only `Size`.

Contract:

```text
Size present and integer → configured value
missing file             → 14
missing section/key      → 14
invalid integer          → 14
```

Clean 5517 resolves this value to `12`.

The two height defaults are intentionally distinct:

```text
Font.ini nominal-height fallback = 12
info.ini normal-height fallback  = 14
```

`info.ini` remains a loose-file configuration source.

### Client Code Page

Retail optionally reads `ini/CodePage.ini`.

The first line supplies the configured native code-page value. Later font-size-level data remains
outside the implemented contract.

The native global begins at:

```text
0
```

On Windows, code page `0` means `CP_ACP`.

Clean 5517 content contains no `CodePage.ini`. Independent decoding of the shipped data establishes
CP936 as the authored encoding, so OpenConquer resolves the clean configuration deterministically:

```text
ConfiguredCodePage = 0
EffectiveCodePage  = 936
```

An explicit nonzero code page is preserved:

```text
CodePage.ini: 950

ConfiguredCodePage = 950
EffectiveCodePage  = 950
```

First-line parsing preserves native-style integer-prefix behavior:

```text
leading ASCII whitespace allowed
optional + / -
consume decimal digits until first non-digit
no digits → 0
```

OpenConquer rejects signed 32-bit overflow rather than reproducing native `atoi` overflow behavior.

`CodePage.ini` is optional, loose-only, and limited to a 256-byte first line.

## Encoded Text Model

The native text path establishes encoded-byte identity before Unicode conversion:

```text
encoded bytes
    ↓
code-page-aware byte walk
    ↓
glyph / newline / data-icon unit
    ↓
native glyph-cache key
    ↓
code-page decoding
    ↓
Unicode scalar
    ↓
FreeType glyph lookup
```

### String Termination

The first NUL terminates the logical input:

```text
41 00 42
↓
glyph 0x0041
end
```

Bytes after the first NUL are ignored.

### Single-Byte Glyphs

A byte not consumed as a DBCS lead byte maps directly to its native glyph key:

```text
41
↓
0x0041
```

### DBCS Glyphs

Lead-byte classification follows the Windows DBCS code-page contract.

| Code page | Lead-byte ranges          |
| --------: | ------------------------- |
|       932 | `81-9F`, `E0-FC`          |
|       936 | `81-FE`                   |
|       949 | `81-FE`                   |
|       950 | `81-FE`                   |
|      1361 | `84-D3`, `D8-DE`, `E0-F9` |

Other code pages have no DBCS lead-byte ranges at this boundary.

A lead byte followed by another logical byte consumes both:

```text
key = (lead << 8) | trail
```

Verified CP936 example:

```text
B0 D7
↓
0xB0D7
```

The trail byte is not independently validated during byte walking.

A dangling lead byte remains a single-byte glyph:

```text
B0
↓
0x00B0
```

A following NUL also leaves the lead byte dangling:

```text
B0 00 D7
↓
0x00B0
end
```

### Glyph-Key Decoding

`EncodedGlyphDecoder` converts one established native glyph key into exactly one Unicode scalar.

Single-byte key:

```text
0x0041
↓
41
```

Double-byte key:

```text
0xB0D7
↓
B0 D7
↓
CP936
↓
U+767D 白
```

Decoding fails when:

- the effective code page is invalid, unavailable, or unsupported;
- the encoded bytes are malformed;
- decoding does not produce exactly one Unicode scalar.

Malformed native glyph keys are not repaired or reinterpreted.

### Newline

ASCII line feed is a distinct semantic unit:

```text
0A
↓
newline
```

Layout applies:

```text
X = 0
Y += nominalPixelHeight + nominalPixelHeight / 4
```

## Data Icons

The native measurement path can optionally recognize:

```text
#NN
```

Both `N` bytes must be ASCII decimal digits.

Examples:

```text
#00 → icon 0
#07 → icon 7
#42 → icon 42
#99 → icon 99
```

A valid token consumes exactly three bytes.

Malformed or truncated forms remain ordinary glyph bytes:

```text
#
#0
#7x
#ab
##1
```

When data-icon recognition is disabled:

```text
#07
↓
'#'
'0'
'7'
```

Measurement resolves icon width in this order:

```text
nonzero caller width
provider width
16-pixel fallback
```

A missing or zero provider width uses the 16-pixel fallback.

Nonzero caller and provider widths preserve their signed native value.

`NativeTextLayout` can therefore contain ordered data-icon items, but GFX-TEXT-004 does not invent
an icon-texture ownership model. The glyph renderer validates the complete layout before GPU state
mutation and rejects layouts containing data icons.

Actual data-icon resource selection and rendering belong to the UI rendering path that owns those
resources.

## Host Font Discovery

`OpenConquer.Rendering` discovers fonts through each operating system's registered font APIs.

```text
Windows → GDI + DirectWrite
macOS   → CoreText + CoreFoundation
Linux   → fontconfig
```

A discovered font reference contains:

```text
physical file path
optional face index
```

A null face index means the host identified a file but not one specific face.

No adapter recursively scans guessed font directories.

### Windows

Windows enumerates the DirectWrite system font collection and resolves local physical font files and
face indices.

The default GUI candidate begins with:

```text
GetStockObject(DEFAULT_GUI_FONT)
```

Its `LOGFONTW` is translated through DirectWrite to a physical font resource.

If that resource cannot be resolved concretely, the default GUI reference may be absent.

### macOS

macOS enumerates registered font URLs through CoreText.

The default GUI candidate begins with the CoreText system UI font and resolves its file through:

```text
kCTFontURLAttribute
```

This path does not currently provide a required face index, so the reference may use a null face
index.

### Linux

Linux uses an isolated fontconfig configuration and the system font set.

The default GUI candidate is matched from:

```text
sans-serif
```

`FC_FILE` and `FC_INDEX` identify the concrete resource.

Fontconfig sysroot information is honored when constructing physical paths.

## Host Font Catalog

`HostFontCatalog` converts host discovery into deterministic resolver data.

Font paths are canonicalized before identity comparison.

Catalog path identity uses an explicit platform policy:

```text
Windows → case-insensitive
macOS   → ordinal
Linux   → ordinal
```

Catalog ordering is deterministic and independent of host enumeration order.

Duplicate references to the same physical file are merged.

If any registration for a file has an unknown face index, all inspectable faces in that file are
eligible. Otherwise only explicitly registered face indices are eligible.

The default GUI resource is included even when absent from the ordinary host enumeration.

FreeType inspection supplies family and style metadata.

Invalid, inaccessible, empty, or otherwise uninspectable registered files remain known candidate
files without producing family entries.

For a default GUI reference with an explicit face index, the concrete resource remains a valid
candidate even when metadata inspection fails.

For a default GUI reference without a face index:

```text
regular face preferred
otherwise first deterministic inspectable face
no inspectable face → no resolved default GUI font
```

## Font Resolution

`SystemFontResolver` handles family and file-token requests separately.

### Family Requests

Family lookup is case-insensitive.

When multiple faces share a family:

```text
regular face preferred
otherwise deterministic first catalog entry
```

The selected face index is preserved.

### File Requests

A leading `$` selects registered-file resolution:

```text
$simsun.ttc
```

Matching uses the registered physical filename and is case-insensitive.

Directory separators are rejected so file tokens cannot become arbitrary filesystem paths.

The current verified file-token contract does not carry a collection face index:

```text
ResolvedFont(filePath, faceIndex: 0)
```

### Default GUI Font

Default GUI resolution uses the concrete host resource discovered by the platform adapter.

It is not converted back into a family name for another resolution pass.

## Native Font Creation Fallback

Resolution and face creation are separate stages.

Creation attempts occur in this exact order:

```text
1. requested face/token
2. host default GUI font
3. $simsun.ttc
4. $simsun.ttf
5. Courier New
```

Rules:

```text
null requested token  → skip requested stage
empty requested token → skip requested stage
whitespace token      → attempt normally
unresolved candidate  → continue
recoverable creation failure → continue
successful creation   → stop
```

Recoverable creation failures are limited to:

```text
FontFaceCreationException
InvalidDataException
IOException
UnauthorizedAccessException
```

Unexpected ABI, lifetime, arithmetic, resource-exhaustion, or programming failures propagate.

The fallback chain is not deduplicated. Two stages resolving to the same physical face remain two
policy attempts.

If no usable candidate exists, creation fails explicitly.

This policy is only for font creation. Missing-glyph fallback after face creation is handled by the
native glyph-cache layer.

## FreeType Boundary

`OpenConquer.Rendering` owns a narrow FreeType native seam.

The managed ABI declarations model the supported FreeType 2.x ABI and do not escape the
interop/rasterization boundary.

Runtime requirement:

```text
FreeType 2.x >= 2.14.3
```

The packaged native dependency currently provides FreeType 2.14.3.

When `FreeTypeLibrary` initializes, it queries the loaded version and rejects:

```text
major != 2
version < 2.14.3
```

### ABI Portability

Native C `long` width differs across supported 64-bit platforms:

```text
Unix 64-bit C long     = 64-bit
Windows 64-bit C long  = 32-bit
```

The FreeType seam therefore uses explicit `CLong` and `CULong` representations.

ABI layout tests verify the native structures consumed by the rasterizer on supported CI platforms.

### Lifetime

`FreeTypeLibrary` owns one initialized native library.

`FreeTypeFace` owns:

```text
FT_Face
mapped font file
library lease
```

A face's library lease keeps FreeType alive even if the original `FreeTypeLibrary` owner is
disposed.

Destruction order:

```text
FT_Face
mapped font file
library lease
```

Failed construction releases partially acquired resources.

## Glyph Rasterization

`FreeTypeGlyphRasterizer` owns one configured face.

Face sizing uses:

```text
FT_Set_Char_Size(
    face,
    0,
    nominalPixelHeight << 6,
    0,
    0)
```

The `× 64` conversion uses checked arithmetic.

```text
height == 0 → rejected
negative nonzero height → preserved and passed through
```

### Glyph Lookup

Unicode scalars are mapped through FreeType's active character map.

A zero glyph index means missing:

```text
TryRasterizeGlyph → false
glyph             → null
```

The rasterizer does not choose a fallback font.

### Metrics

Managed metrics preserve the verified native semantics:

```text
bearing X  = bitmap_left
top offset = nominalPixelHeight - bitmap_top
advance    = horizontal 26.6 advance / 64
```

Signed advance conversion truncates toward zero.

`RasterizedGlyph` also carries:

```text
bitmap width
bitmap height
normalized coverage
```

A valid space can therefore have:

```text
bitmap area = 0
advance     > 0
```

### Bitmap Normalization

FreeType bitmap pitch can be positive or negative. Logical row traversal therefore depends on the
pitch sign:

```text
pitch >= 0:
    row pointer = buffer + row * pitch

pitch < 0:
    row pointer = buffer + (rows - 1 - row) * abs(pitch)
```

For negative pitch, `buffer` addresses the lower physical row and logical traversal proceeds in the
opposite physical direction.

Output coverage is tightly packed in logical row order.

### Antialiasing

Enabled:

```text
FreeType render mode = normal
coverage             = normalized grayscale
```

Disabled:

```text
FreeType render mode = mono
coverage             = 0 or 255
```

The rasterizer exposes this immutable configuration through `IGlyphRasterizer.AntialiasEnabled`.
`NativeTextFontRecord` carries the same authoritative policy into rendering.

The verified native draw behavior when antialiasing is disabled is:

```text
text color alpha   → 255
corner color alpha → 255
```

Persistent four-corner colors used by `PerCornerColor` remain separate and are not force-opaqued by
that rule.

OpenConquer derives this behavior from the font/rasterizer configuration. There is no independent
caller-controlled antialias boolean in the rendering API.

## Missing-Glyph Fallback

Font creation fallback and missing-glyph fallback are separate policies.

Each native glyph cache is configured with:

```text
primary font record
font record 0
effective code page
```

For an uncached encoded glyph key:

```text
decode glyph key
try primary font record

if missing and primary record != 0:
    try font record 0

if still missing:
    advance using primary line-height behavior
```

When the primary font is already record 0, the fallback record is not retried.

A glyph-key decoding failure is treated as missing and uses the primary line-height advance without
calling either rasterizer.

Unexpected rasterizer failures propagate rather than being converted into missing-glyph results.

## Native Glyph Cache

The cache key remains the native encoded glyph key:

```text
ushort encoded glyph key
```

Unicode decoding occurs only after a cache miss.

Cached entries preserve:

```text
source font-record index
bearing-left
top offset
advance
optional atlas region
missing state
```

Successful zero-area glyphs such as spaces are cached with their advance but without atlas storage.

Missing results are cached so repeated unsupported glyphs do not repeatedly perform decoding or font
lookup.

Coverage bytes are stored by the atlas rather than duplicated in every cache entry.

The cache does not own or dispose the supplied font rasterizers.

## Glyph Atlas

Verified native atlas requirements:

```text
page width  = 512 pixels
page height = 512 pixels
separation  = 2 pixels
```

Zero-area glyphs do not consume atlas storage.

Glyphs larger than one atlas page fail explicitly.

OpenConquer currently uses deterministic shelf allocation inside those verified page and separation
constraints.

Exact native row-wrap and page-allocation thresholds have not been established, so OpenConquer does
not claim that its internal atlas coordinates are native-identical.

Each page exposes normalized grayscale coverage and a monotonically increasing revision.

GFX-TEXT-004 mirrors each CPU page to a lazily created OpenGL `R8` texture. The GPU copy retains the
CPU page's last uploaded revision and performs a full-page `TexSubImage2D` synchronization when that
revision changes.

Texture policy is:

```text
internal format = R8
min filter      = nearest
mag filter      = nearest
wrap S/T        = clamp-to-edge
base level      = 0
max level       = 0
mipmaps         = none
```

Coverage is sampled explicitly from the texture's red channel.

## Native Text Layout

Layout consumes `EncodedTextReader` tokens rather than reparsing encoded bytes.

The pen begins at:

```text
X = 0
Y = 0
```

### Glyphs

Drawable glyph position:

```text
X = pen X + bearing-left
Y = pen Y + top offset
```

After processing the glyph:

```text
pen X += advance
```

A successful zero-area glyph advances the pen without emitting a drawable glyph item.

A missing glyph advances using the cached primary line-height behavior and emits no drawable item.

### Newlines

On newline:

```text
maximum width = max(maximum width, pen X)
pen X         = 0
pen Y        += nominal height + nominal height / 4
```

### Measurement

Final dimensions are:

```text
width  = maximum horizontal pen extent
height = accumulated newline advance + nominal height
```

An empty string therefore measures:

```text
width  = 0
height = nominal height
```

Layout arithmetic uses checked integer operations.

### Data Icons

A recognized data icon emits one ordered data-icon layout item carrying:

```text
icon index
X
Y
resolved width
```

Its width advances the horizontal pen.

The glyph renderer does not attempt to reinterpret a data icon as text. Until an explicit
icon-resource rendering path exists, such layouts are rejected before OpenGL state is modified.

## Layout Provenance

A glyph atlas page index is meaningful only relative to the exact glyph atlas that produced it.
Rendering must also use the font configuration that supplied the layout's antialias policy.

`NativeTextLayoutSource` therefore binds:

```text
authoritative primary font record
authoritative CPU glyph atlas
```

Each `NativeTextLayoutEngine` creates one stable source instance and every layout produced by that
engine retains that exact source identity.

The identity contract is reference identity rather than structural equivalence:

```text
layout.Source must be the exact source bound to the GPU text resource
```

`NativeTextLayoutSource` is provenance only. It does not own or dispose the supplied font record,
rasterizer, glyph cache, or atlas.

`OpenGLTextResource` owns the OpenGL atlas mirror corresponding to exactly one layout source and one
OpenGL device.

Before drawing, rendering validates:

```text
renderer/device identity
text-resource device identity
layout/source identity
layout item kinds
atlas page validity
render-style work budget
```

These deterministic caller failures occur before text-pipeline OpenGL state mutation.

## Native Render Styles

The verified native `RENDER_TEXT_STYLE` values are:

```text
0 = Normal
1 = ShadowOffset
2 = MultiOffsetOutline
3 = OffsetTrail
4 = PerCornerColor
```

### Normal

One base pass:

```text
offset = (0, 0)
color  = text color
```

### ShadowOffset

Two passes:

```text
1. caller corner offset using corner color
2. base text
```

### MultiOffsetOutline

Eight one-pixel corner passes followed by the base text.

Verified order:

```text
(-1,  0)
( 1,  0)
( 0, -1)
( 0,  1)
(-1, -1)
( 1, -1)
(-1,  1)
( 1,  1)
( 0,  0) base
```

### OffsetTrail

The native path emits deterministic interpolated offsets from the configured displacement back
toward the base position, then emits the base pass.

Pass count:

```text
abs(offset X) + abs(offset Y) + 1
```

Verified example:

```text
offset = (3, 1)

(3, 1)
(2, 1)
(2, 0)
(1, 0)
(0, 0) base
```

Signed offsets are preserved.

### PerCornerColor

One base geometry pass using four independently configured colors.

Native corner order is:

```text
top-left
bottom-left
top-right
bottom-right
```

The four persistent corner colors remain independent from the ordinary text/corner-alpha
force-opaque behavior used when antialiasing is disabled.

## Native Glyph Geometry

Each glyph render pass emits six vertices as two triangles.

Verified native vertex order:

```text
triangle 1:
top-left
bottom-left
top-right

triangle 2:
bottom-right
top-right
bottom-left
```

Equivalent sequence:

```text
TL, BL, TR, BR, TR, BL
```

Per-vertex color mapping therefore follows:

```text
TL, BL, TR, BR, TR, BL
```

The `TR ↔ BL` diagonal is intentionally preserved because it changes per-corner color interpolation.

Texture coordinates are normalized directly from the glyph's atlas region.

Destination arithmetic uses widened integer intermediates before conversion to floating-point
coordinates.

### Half-Pixel Boundary

The D3D8-era implementation contains coordinate representation details associated with its graphics
API.

OpenConquer does not reproduce a D3D8 half-pixel offset mechanically.

The modern OpenGL path expresses glyph edges directly in logical pixel coordinates and maps them to
clip space:

```text
xNdc = (x / targetWidth) * 2 - 1
yNdc = 1 - (y / targetHeight) * 2
```

No `±0.5` correction is introduced.

This preserves the observable logical placement verified by real-driver conformance without
cargo-culting an obsolete API representation artifact.

## OpenGL Text Pipeline

GFX-TEXT-004 uses a dedicated OpenGL 3.3 Core-compatible text pipeline.

Vertex attributes are tightly packed:

```text
offset  0: vec2 position          float
offset  8: vec2 texture coordinate float
offset 16: vec4 color             normalized unsigned byte

stride = 20 bytes
```

The vertex ABI is covered by exact managed size and field-offset tests.

The fragment contract is:

```text
coverage = texture(atlas, uv).r
output RGB   = vertex RGB
output alpha = vertex alpha * coverage
```

The atlas is coverage-only. Texture RGB is not used to modulate the output RGB channels.

Relevant draw state is explicitly established:

```text
scissor test  = disabled
depth test    = disabled
depth write   = disabled
cull          = disabled
dither        = disabled
framebuffer sRGB = disabled
color mask    = RGBA enabled
blend         = enabled
blend equation = add
source factor = source alpha
dest factor   = one minus source alpha
active texture = unit 0
```

This is the modern equivalent of the verified native text state:

```text
alpha blend = enabled
SrcAlpha / InvSrcAlpha
depth test/write = disabled
alpha test = disabled
dither = disabled
culling = disabled
lighting = disabled
Gouraud shading
solid fill
point min/mag filtering
no mipmaps
texture RGB selects diffuse
texture alpha multiplies diffuse alpha
```

Fixed-function state, FVF declarations, COM ownership, and D3D8 pipeline structure are not
reproduced.

After drawing, the text pipeline returns the renderer to its defined local baseline rather than
depending on incidental prior OpenGL state.

## Text Batching

Native evidence establishes that batched text rendering:

```text
collects vertices by atlas page
flushes atlas-page batches at Font_DrawEnd
```

The exact native atlas-page traversal order during the final flush has not been established.

OpenConquer therefore does not claim parity for an undocumented page-flush ordering.

Its deterministic reconstruction policy is:

```text
atlas pages flush in ascending stable page index
```

Within each page:

```text
original glyph order is preserved
render-pass order for each glyph is preserved
```

Page indices are deterministic atlas creation-order identities.

Cross-page overlap remains a documented uncertainty because a different native final page traversal
could make overlap ordering observable.

## Bounded Rendering Work

Rendering uses reusable fixed-capacity CPU and GPU staging resources.

Current bounds:

```text
staging capacity      = 4,096 glyph passes
maximum draw workload = 65,536 glyph passes
vertices per pass     = 6
```

A staging-capacity boundary causes a flush and reuse of the same storage. It is not a text-length
compatibility limit.

The maximum workload is a modern resource-safety policy that prevents pathological trail offsets or
untrusted text from creating unbounded CPU work.

For reference, the nine-pass outline style can render:

```text
floor(65,536 / 9) = 7,281 glyphs
```

in one render operation before reaching that safety boundary.

The bound is not claimed to be a native client restriction.

Normal warmed staging and batching paths reuse previously allocated storage. Managed allocation is
permitted when capacities or atlas resources grow for the first time.

## OpenGL Resource Ownership

Ownership is intentionally separated:

```text
NativeTextLayoutSource
    authoritative CPU font/atlas provenance
    owns neither object

OpenGLTextResource
    one-device GPU mirror of exactly one NativeTextLayoutSource
    owns OpenGLGlyphAtlas
    does not own CPU font/cache/atlas

OpenGLTextRenderer
    owns reusable rendering pipeline
    owns reusable CPU staging storage
    owns reusable VBO/VAO resources
    owns reusable batch planner
    owns no font/cache/layout source

OpenGLRenderer
    owns one OpenGLTextRenderer
    consumes independently owned OpenGLTextResource instances

OpenGLGraphicsDevice
    creates text resources for its GL device
```

GPU objects must be disposed while their OpenGL context remains valid.

As with existing OpenGL texture resources, device identity is validated explicitly rather than
allowing resources created by one graphics device to be used by another.

Renderer construction and destruction follow explicit reverse ownership ordering and preserve the
first encountered failure during cleanup.

## Failure Atomicity

Deterministic request validation occurs before the first text draw-state mutation.

This includes:

```text
renderer disposal state
resource disposal state
resource/device ownership
layout/source provenance
target dimensions
data-icon rejection
atlas page references
render-style expansion
maximum glyph-pass workload
batch-plan construction
```

An empty drawable layout is a true no-op and does not activate the text pipeline.

Once GPU execution begins, driver/backend failures can naturally occur after partial GPU work. The
renderer performs best-effort deterministic cleanup while preserving the first failure rather than
masking it with a secondary cleanup exception.

## Managed Ownership

### `OpenConquer.Content`

```text
GameFontConfiguration
    ini/Font.ini
    face token
    nominal pixel height

ClientFontSizeConfiguration
    ini/info.ini
    [FontSize] Size

ClientCodePageConfiguration
    optional ini/CodePage.ini
    configured native value
    deterministic effective value
```

### `OpenConquer.Rendering`

```text
DbcsLeadByteClassifier
EncodedTextToken
EncodedTextReader
EncodedGlyphDecoder

HostFontReference
HostFontDiscovery
IHostFontSource
SystemHostFontSource
WindowsHostFontSource
MacOSHostFontSource
LinuxHostFontSource

HostFontCatalog
IFontResolver
SystemFontResolver
ResolvedFont

FreeTypeLibrary
FreeTypeFontFile
FreeTypeFontInspector
FreeTypeFace
FreeTypeBitmapNormalizer

IGlyphRasterizer
FreeTypeGlyphRasterizer
FreeTypeGlyphRasterizerFactory
RasterizedGlyph

NativeTextFontRecord
CachedGlyph
NativeGlyphCache

GlyphAtlasRegion
GlyphAtlasPage
GlyphAtlas

IDataIconWidthProvider
NativeTextLayoutItem
NativeTextLayoutSource
NativeTextLayout
NativeTextLayoutEngine

NativeTextRenderStyle
NativeTextRenderOptions
NativeTextVertexColors
NativeTextRenderPass
NativeTextRenderPassSequence
NativeTextVertex
NativeTextGeometryBuilder
NativeTextBatchPlan

OpenGLGlyphAtlasPageTexture
OpenGLGlyphAtlas
OpenGLTextResource
OpenGLTextVertex
OpenGLTextVertexStagingBuffer
OpenGLTextVertexBuffer
OpenGLTextPipeline
OpenGLTextRenderer
```

Content and Rendering remain independent sibling projects.

Host font APIs are rendering-resource discovery mechanisms and remain inside Rendering. They do not
introduce a Rendering → Platform dependency.

The Client composition root will connect Content-derived configuration to Rendering when a runtime
text consumer exists.

## GFX-TEXT-002 Scope

Implemented and verified:

```text
Font.ini contract
info.ini normal font-height contract
CodePage.ini first-line contract
configured code-page preservation
deterministic clean-5517 CP936 resolution

DBCS lead-byte classification
single-byte glyph keys
double-byte glyph keys
NUL termination
newline recognition
dangling lead-byte behavior
optional #NN recognition
data-icon indices 00-99
glyph-key → Unicode scalar decoding

Windows registered-font discovery
macOS registered-font discovery
Linux registered-font discovery
host default-GUI resource discovery
deterministic host font catalog

family resolution
$*.tt? registered-file resolution
native font-creation fallback order

FreeType 2.x ABI enforcement
FreeType 2.14.3 minimum-version enforcement
portable C ABI representation
native library/face lifetime
font metadata inspection

grayscale bitmap normalization
monochrome bitmap normalization
glyph rasterization
glyph metrics seam
space advance preservation
missing-glyph reporting
antialias mode selection
```

Deferred work from GFX-TEXT-002 is implemented by later slices rather than being incomplete work in
that slice.

## GFX-TEXT-003 Scope

Implemented and verified:

```text
native font-record layout model
missing-glyph fallback to font record index 0
primary line-height missing-glyph advance
encoded-key glyph cache
cached missing-glyph results
cached zero-area glyph metrics

512x512 glyph-atlas pages
2-pixel glyph separation
deterministic managed atlas allocation
multi-page atlas growth
zero-area atlas bypass
atlas page revision tracking

native text measurement
glyph destination layout
newline X reset
newline Y advance
space advance without drawable geometry
ordered glyph/data-icon layout items

optional data-icon recognition
explicit nonzero data-icon width override
provider data-icon width lookup
16-pixel missing/zero width fallback
signed nonzero data-icon width preservation

checked layout and atlas arithmetic
```

Later rendering work from this slice is implemented by GFX-TEXT-004 rather than being incomplete
GFX-TEXT-003 work.

Still outside the implemented compatibility contract:

```text
CodePage.ini font-size levels
```

## GFX-TEXT-004 Scope

Implemented and verified:

```text
verified native text render-style values 0-4
normal style
shadow-offset style
eight-pass outline ordering
offset-trail ordering
per-corner vertex coloring

font-derived antialias render policy
AA-disabled text alpha forcing
AA-disabled corner alpha forcing
independent persistent per-corner colors

logical glyph geometry
native six-vertex triangle ordering
native TR-BL interpolation diagonal
logical pixel-edge positioning
modern no-half-pixel OpenGL mapping

layout-source provenance
exact CPU atlas/font identity binding
OpenGL resource/device identity validation

R8 coverage-only OpenGL atlas textures
512x512 GPU page mirrors
nearest filtering
no mipmaps
CPU atlas revision synchronization

20-byte packed OpenGL text vertex ABI
OpenGL 3.3 Core text shaders
coverage × vertex-alpha fragment behavior
SrcAlpha / OneMinusSrcAlpha blending
text-specific depth/cull/dither state

native per-page batching model
deterministic ascending-page reconstruction policy
stable glyph ordering within each page
stable render-pass ordering within each glyph

bounded reusable CPU staging
bounded reusable VBO storage
bounded total glyph-pass workload
warmed staging/batch-plan zero-managed-allocation behavior

pre-GPU data-icon rejection
pre-GPU layout/source validation
pre-GPU work-budget validation
first-failure-preserving cleanup

real-driver synthetic native-text conformance
coverage/placement conformance
alpha/coverage multiplication conformance
per-corner native-diagonal conformance
atlas-revision synchronization conformance
```

Known evidence boundary:

```text
native batches by atlas page: verified
exact native final page traversal order: unresolved
OpenConquer ascending page-index order: deterministic modern policy
```

GFX-TEXT-004 deliberately does not add the first runtime HUD/UI consumer. That composition belongs
to GFX-UI-001.

## Text Reconstruction Roadmap

```text
GFX-TEXT-001
retail text configuration
+ encoded-byte semantics
        ↓
GFX-TEXT-002
font resolution
+ font creation fallback
+ rasterizer / glyph-metrics seam
        ↓
GFX-TEXT-003
native measurement/layout
+ missing-glyph fallback
+ glyph cache / atlas
        ↓
GFX-TEXT-004
OpenGL text rendering
+ real-driver conformance
        ↓
GFX-UI-001
first native UI consumer
```

The intended first UI consumer remains the verified status-hint panel.

## Conformance Boundary

Deterministic policy and CPU contracts are covered by unit tests.

Host-font adapters require execution on their real operating systems. CI validates the native host
and FreeType seams on supported Windows, macOS, and Linux environments.

GFX-TEXT-004 adds real-driver OpenGL conformance using synthetic deterministic glyph coverage rather
than host-selected font output.

The text conformance path verifies:

```text
logical pixel placement
nearest coverage sampling
coverage × diffuse alpha
SrcAlpha / OneMinusSrcAlpha blending
native per-corner TR-BL triangle diagonal
CPU atlas revision → existing GPU texture synchronization
```

The synthetic fixture populates `GlyphAtlas` directly. It does not invoke FreeType or depend on host
font discovery.

This separation is intentional.

The exact FreeType revision and exact physical font revision used by the retail client have not been
established. OpenConquer therefore does not claim pixel-identical glyph bitmaps merely because both
implementations use FreeType-shaped rasterization.

Exact rasterized glyph pixels can vary with:

```text
font revision
FreeType revision
hinting behavior
host font selection
```

The implemented GFX-TEXT-004 conformance instead establishes the deterministic rendering behavior
after coverage has entered the authoritative OpenConquer glyph atlas.

## Modernization Boundary

Preserve verified 5517 behavior:

```text
encoded-byte traversal before Unicode conversion
native one/two-byte glyph-cache keys
optional #NN recognition
configured code-page semantics
evidence-backed CP936 clean-client default

$*.tt? file-token semantics
exact font-creation fallback order
negative nonzero requested font heights
verified FreeType metric semantics
grayscale versus monochrome rasterization

record-zero missing-glyph retry
primary line-height missing-glyph advance
newline reset/advance behavior
nonzero signed data-icon width semantics

512x512 glyph atlas
2-pixel atlas separation

render-style values and semantics
outline pass ordering
trail pass ordering
native four-corner ordering
native six-vertex triangle ordering
native TR-BL interpolation diagonal

AA-disabled text/corner alpha behavior
coverage-only atlas semantics
nearest text sampling
source-alpha blending
per-page batching behavior
```

Modern implementation constraints:

```text
Content owns configuration parsing
Rendering owns text and font behavior
Content and Rendering remain independent
Rendering and Platform remain independent siblings

use authoritative host font systems
do not recursively scan guessed font directories
do not allow font tokens to become arbitrary paths

use explicit native C ABI widths
make native ownership and destruction explicit
require FreeType 2.x >= 2.14.3
bound configuration inputs
reject arithmetic overflow deterministically
do not depend on host ANSI settings for clean-client behavior

do not expand runtime content without a consumer
do not conflate font-creation fallback with missing-glyph fallback
do not put missing-glyph fallback into the font-creation factory

do not put GPU ownership into measurement/cache/atlas policy
bind GPU atlas resources to exact layout-source provenance
validate deterministic rendering failures before GPU mutation
use bounded reusable staging and GPU buffers
avoid warmed-path managed allocation where practical

use modern OpenGL shaders rather than fixed-function D3D emulation
use R8 coverage rather than recreating legacy texture representation
express final pixel-edge coordinates directly
do not recreate D3D8 half-pixel representation artifacts
do not recreate COM, FVF, or fixed-function ownership structure

do not claim undocumented atlas coordinates as native behavior
do not claim undocumented atlas-page flush order as native behavior
do not claim retail glyph-bitmap pixel identity without evidence
```

The governing rule is observable compatibility rather than implementation archaeology:

```text
preserve behavior that affects visible output, content compatibility, protocol semantics,
or deterministic gameplay/client behavior

modernize obsolete API representation and incidental native implementation machinery
when doing so does not change the verified observable contract
```
