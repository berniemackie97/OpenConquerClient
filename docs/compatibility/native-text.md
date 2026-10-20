# 5517 Text Compatibility

This document describes how OpenConquer reproduces the text handling of the Conquer Online 5517
client: text configuration files, encoded-text rules, font resolution, glyph rasterization, caching,
atlas storage, layout, render styles, OpenGL resource ownership and GPU drawing.

Addresses in the retail binaries, decompilation traces, caller lists and open reverse-engineering
questions are kept in the native analysis notes rather than here.

## Startup Configuration

### Game Font

The retail client reads its initial game font from `ini/Font.ini`. The file has this format:

```text
<face token><space><nominal pixel height>
```

The parser splits at the last ASCII space, so face names may contain spaces. The clean 5517 install
contains:

```text
Arial 12
```

Parsing rules:

```text
face token            = bytes before final ASCII space
nominal pixel height  = integer suffix
invalid/zero height   = 12
negative nonzero      = preserved
```

The Content layer keeps the face token byte for byte.

`Font.ini` is read only as a loose file. A copy inside a WDF package is not used.

OpenConquer rejects a `Font.ini` larger than 256 bytes. The retail client instead reads the file
into a fixed-size buffer and truncates it.

`Font.ini` is part of the runtime content set because the selected-skill cooldown text (GFX-UI-007)
is the first HUD element in production that draws with this font pipeline.

### GUI Font Settings

If a loose `ini/FontSetting.ini` exists, the retail client reads it to override GUI and chat font
settings. The clean 5517 install does not include this file.

Before the file is read, the settings start from the retail client's initial values, which
`ClientFontSettingsConfiguration` models:

```text
chat font height       14
render style           0
antialias               false
GUI shadow              false
chat shadow color       0x00000000
corner color            0x00000000
corner offset           (1,1)
GUI face                Font.ini face
chat face               inherited GUI face when empty
```

Recognized keys:

```text
GUIFontShadow
GUIFontShadowColor
ChatFont
ChatFontSize
ChatFontShadowColor
Antialias
GUIFont
```

Parsing quirks confirmed in the retail client:

```text
keys are case-insensitive
key/value whitespace is not normalized

GUIFontShadow=1
→ GUI shadow true
→ render style becomes 1

later GUIFontShadow=0
→ GUI shadow false
→ render style remains 1

Antialias is true only when atoi(value) == 1

ChatFontSize=0
→ 16

initial missing ChatFontSize
→ 14

shadow colors use base-16 unsigned parsing

empty ChatFont after parsing
→ inherit GUIFont
```

For malformed or extremely large numbers, OpenConquer does not match the overflow behavior of the
retail C runtime. Compatibility covers only the value ranges that were checked against the retail
client.

### Font Tokens

A font request is either a family name or a file token of this form:

```text
$*.tt?
```

The leading `$` marks a file token. Examples:

```text
$simsun.ttc
$simsun.ttf
```

The `?` stands for the third character of the extension. OpenConquer does not treat it as a
wildcard. An accepted extension has three characters, and the first two are `tt`.

File tokens match only fonts registered with the host's font system. They never start a recursive
directory search or open an arbitrary path.

### Normal UI Font Height

Retail `ini/info.ini` contains:

```ini
[FontSize]
Size=12
Width=6
```

Only `Size` is used today.

Resolution rules:

```text
Size present and integer → configured value
missing file             → 14
missing section/key      → 14
invalid integer          → 14
```

The clean 5517 install resolves this value to `12`.

The two fallback heights differ, and both are correct:

```text
Font.ini nominal-height fallback = 12
info.ini normal-height fallback  = 14
```

`info.ini` is also read only as a loose file.

### Client Code Page

The retail client reads `ini/CodePage.ini` if it exists.

The first line holds the configured code page. The font-size-level data on later lines is not
implemented.

The retail client's code-page global starts at:

```text
0
```

On Windows, code page `0` means `CP_ACP`, the system ANSI code page.

The clean 5517 install has no `CodePage.ini`. Decoding the shipped data independently shows that
it was written in CP936, so OpenConquer resolves the clean configuration to CP936 regardless of the
host's ANSI code page:

```text
ConfiguredCodePage = 0
EffectiveCodePage  = 936
```

A nonzero configured code page is used as is:

```text
CodePage.ini: 950

ConfiguredCodePage = 950
EffectiveCodePage  = 950
```

The first line is parsed the way the retail client parses an integer prefix:

```text
leading ASCII whitespace allowed
optional + / -
consume decimal digits until first non-digit
no digits → 0
```

A value that overflows a signed 32-bit integer is rejected. OpenConquer does not reproduce the
overflow behavior of the retail `atoi` call.

`CodePage.ini` is optional and read only as a loose file. Its first line may be at most 256 bytes.

## Encoded Text Model

Text is handled as encoded bytes. The identity of each glyph comes from those bytes, before any
conversion to Unicode:

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

The first NUL byte ends the string:

```text
41 00 42
↓
glyph 0x0041
end
```

Bytes after the first NUL are ignored.

### Single-Byte Glyphs

A byte that is not used as a DBCS lead byte becomes its own glyph key:

```text
41
↓
0x0041
```

### DBCS Glyphs

Lead bytes are classified with the Windows DBCS code-page ranges:

| Code page | Lead-byte ranges          |
| --------: | ------------------------- |
|       932 | `81-9F`, `E0-FC`          |
|       936 | `81-FE`                   |
|       949 | `81-FE`                   |
|       950 | `81-FE`                   |
|      1361 | `84-D3`, `D8-DE`, `E0-F9` |

All other code pages are treated as having no lead bytes.

When a lead byte is followed by another byte of the string, the two bytes form one key:

```text
key = (lead << 8) | trail
```

CP936 example:

```text
B0 D7
↓
0xB0D7
```

The byte walk does not validate the trail byte.

A lead byte at the end of the string stays a single-byte glyph:

```text
B0
↓
0x00B0
```

The same happens when the lead byte is followed by NUL:

```text
B0 00 D7
↓
0x00B0
end
```

### Glyph-Key Decoding

`EncodedGlyphDecoder` converts one glyph key into a single Unicode scalar value.

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

- the effective code page is invalid, unavailable or unsupported
- the encoded bytes are malformed
- decoding produces anything other than one Unicode scalar

A malformed glyph key is never repaired or reinterpreted.

### Newline

An ASCII line feed is a token of its own:

```text
0A
↓
newline
```

Layout then moves the pen:

```text
X = 0
Y += nominalPixelHeight + nominalPixelHeight / 4
```

## Data Icons

The retail measurement code can optionally recognize data-icon tokens:

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

A valid token is three bytes long.

Malformed or truncated forms are treated as ordinary glyph bytes:

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

Measurement takes the icon width from the first of these that applies:

```text
nonzero caller width
provider width
16-pixel fallback
```

A missing or zero provider width uses the 16-pixel fallback.

Nonzero caller and provider widths are used as signed values, as in the retail client, so a
negative width stays negative.

A `NativeTextLayout` can contain data-icon items in text order. GFX-TEXT-004 does not
define who owns icon textures, so the glyph renderer checks the whole layout before it changes any
GPU state and rejects a layout that contains data icons.

Choosing and drawing the icon images is the job of the UI rendering code that owns those resources.

## Host Font Discovery

`OpenConquer.Rendering` finds fonts through the font registration APIs of each operating system:

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

A null face index means the host named a file but not a specific face in it.

No adapter recursively scans guessed font directories.

### Windows

The Windows adapter enumerates the DirectWrite system font collection and resolves each entry to a
local font file and face index.

The default GUI font starts from:

```text
GetStockObject(DEFAULT_GUI_FONT)
```

DirectWrite translates its `LOGFONTW` into a physical font resource.

If that translation does not produce a specific resource, there may be no default GUI reference.

### macOS

The macOS adapter enumerates registered font URLs through CoreText.

The default GUI font is the CoreText system UI font. Its file comes from:

```text
kCTFontURLAttribute
```

This lookup does not currently provide a face index, so the reference may have a null face index.

### Linux

The Linux adapter uses its own isolated fontconfig configuration together with the system font
set.

The default GUI font is the fontconfig match for:

```text
sans-serif
```

`FC_FILE` and `FC_INDEX` give the file and face.

Physical paths include the fontconfig sysroot when one is set.

## Host Font Catalog

`HostFontCatalog` turns the discovered fonts into data for the resolver.

Font paths are normalized before they are compared.

Path comparison depends on the platform:

```text
Windows → case-insensitive
macOS   → ordinal
Linux   → ordinal
```

Catalog order is stable and does not depend on the order in which the host lists fonts.

Duplicate references to the same physical file are merged.

If any registration of a file has no face index, every inspectable face in that file is eligible.
Otherwise only the registered face indices are eligible.

The default GUI font is included even when the normal host enumeration does not list it.

FreeType inspection provides each face's family and style metadata.

A registered file that is invalid, inaccessible, empty or otherwise cannot be inspected stays in the
catalog as a candidate file, but it adds no family entries.

When the default GUI reference has a face index, that file and face remain a valid candidate even if
metadata inspection fails.

When the default GUI reference has no face index:

```text
regular face preferred
otherwise first deterministic inspectable face
no inspectable face → no resolved default GUI font
```

## Font Resolution

`SystemFontResolver` handles family requests and file-token requests separately.

### Family Requests

Family lookup is case-insensitive.

When several faces share a family:

```text
regular face preferred
otherwise deterministic first catalog entry
```

The result keeps the chosen face index.

### File Requests

A leading `$` asks for a registered font file by name:

```text
$simsun.ttc
```

The token is compared case-insensitively with the file name of each registered font.

Tokens that contain directory separators are rejected, so a file token can never become an
arbitrary file system path.

A file token has no way to name a face inside a font collection, so it always resolves to face 0:

```text
ResolvedFont(filePath, faceIndex: 0)
```

### Default GUI Font

The default GUI font resolves directly to the file and face that the platform adapter found.

It is not turned back into a family name and resolved a second time.

## Font Creation Fallback

Resolving a font and creating a face from it are separate steps.

Creation tries these candidates in this order:

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

Only these exceptions count as recoverable creation failures:

```text
FontFaceCreationException
InvalidDataException
IOException
UnauthorizedAccessException
```

Other failures (ABI, lifetime, arithmetic, resource exhaustion or programming errors) propagate to
the caller.

The chain is not deduplicated. If two stages resolve to the same physical face, creation is
attempted twice.

If no candidate works, font creation fails with an error.

This chain applies only to font creation. Fallback for missing glyphs happens after the face exists,
in the glyph cache.

## FreeType Interop

`OpenConquer.Rendering` contains a small native interop layer for FreeType.

Its managed declarations describe the supported FreeType 2.x ABI and are not exposed outside the
interop and rasterization code.

Required version:

```text
FreeType 2.x >= 2.14.3
```

The bundled native package currently ships FreeType 2.14.3.

The originally published native binaries of OpenConquer.FreeType.Native 1.0.0 have documented
source and build provenance on all six supported platforms. This does not mean that later Windows
builds reproduce the same bytes. The rendering project copies the FTL.TXT, LICENSE.TXT,
PROVENANCE.md and BDF/PCF notices from the package into `third-party/freetype/` in the published
client. CI checks those notices in both the signed release package and the managed installation.

On initialization, `FreeTypeLibrary` checks the version of the loaded library and rejects:

```text
major != 2
version < 2.14.3
```

### ABI Portability

The size of the C `long` type differs between the supported 64-bit platforms:

```text
Unix 64-bit C long     = 64-bit
Windows 64-bit C long  = 32-bit
```

The interop layer therefore uses `CLong` and `CULong` for these fields.

ABI layout tests check the native structures that the rasterizer reads, on each supported CI
platform.

### Lifetime

`FreeTypeLibrary` owns one initialized native library.

`FreeTypeFace` owns:

```text
FT_Face
mapped font file
library lease
```

The library lease keeps FreeType alive even after the `FreeTypeLibrary` that created the face is
disposed.

These are released in this order:

```text
FT_Face
mapped font file
library lease
```

If construction fails, anything already acquired is released.

## Glyph Rasterization

`FreeTypeGlyphRasterizer` owns one configured face.

The face is sized with:

```text
FT_Set_Char_Size(
    face,
    0,
    nominalPixelHeight << 6,
    0,
    0)
```

The `× 64` conversion uses checked arithmetic. Heights are handled as follows:

```text
height == 0 → rejected
negative nonzero height → preserved and passed through
```

### Glyph Lookup

Unicode scalar values are looked up through the face's active FreeType character map.

Glyph index zero means the glyph is missing:

```text
TryRasterizeGlyph → false
glyph             → null
```

The rasterizer does not choose a fallback font.

### Metrics

Glyph metrics follow the definitions confirmed in the retail client:

```text
bearing X  = bitmap_left
top offset = nominalPixelHeight - bitmap_top
advance    = horizontal 26.6 advance / 64
```

The advance is signed, and the conversion truncates toward zero.

`RasterizedGlyph` also carries:

```text
bitmap width
bitmap height
normalized coverage
```

A space is therefore a valid glyph with:

```text
bitmap area = 0
advance     > 0
```

### Bitmap Normalization

FreeType bitmap pitch can be positive or negative, so the address of each logical row depends on
the sign of the pitch:

```text
pitch >= 0:
    row pointer = buffer + row * pitch

pitch < 0:
    row pointer = buffer + (rows - 1 - row) * abs(pitch)
```

With a negative pitch, `buffer` points at the lower physical row, and logical rows run through
memory in the opposite direction.

The output coverage is tightly packed in logical row order.

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

The rasterizer exposes this setting as `IGlyphRasterizer.AntialiasEnabled`, and it cannot change
after the rasterizer is created. `NativeTextFontRecord` carries the same setting into rendering,
where it is the only source of the antialias mode.

When antialiasing is disabled, the retail client draws with:

```text
text color alpha   → 255
corner color alpha → 255
```

The four persistent corner colors used by `PerCornerColor` are separate. This rule does not force
them to be opaque.

OpenConquer takes this behavior from the font and rasterizer configuration. The rendering API has
no separate antialias flag that callers can set.

## Missing-Glyph Fallback

Missing-glyph fallback is a separate policy from font creation fallback.

Each glyph cache is set up with:

```text
primary font record
font record 0
effective code page
```

When an encoded glyph key is not cached yet:

```text
decode glyph key
try primary font record

if missing and primary record != 0:
    try font record 0

if still missing:
    advance using primary line-height behavior
```

If the primary font is already record 0, it is not tried a second time.

If the glyph key cannot be decoded, the glyph is treated as missing. It gets the primary line-height
advance, and neither rasterizer is called.

Unexpected rasterizer failures propagate. They are not turned into missing-glyph results.

## Glyph Cache

As in the retail client, the cache is keyed by the encoded glyph key:

```text
ushort encoded glyph key
```

Unicode decoding happens only on a cache miss.

Each cache entry stores:

```text
source font-record index
bearing-left
top offset
advance
optional atlas region
missing state
```

Glyphs that rasterize with no bitmap area, such as spaces, are cached with their advance and take
no atlas space.

Missing glyphs are cached too, so an unsupported glyph is decoded and looked up only once.

Coverage bytes live in the atlas and are not copied into each cache entry.

The cache does not own the font rasterizers it is given and does not dispose them.

## Glyph Atlas

Atlas parameters confirmed in the retail client:

```text
page width  = 512 pixels
page height = 512 pixels
separation  = 2 pixels
```

Glyphs with zero area take no atlas space.

A glyph larger than one atlas page is an error.

Within those page and separation limits, OpenConquer currently places glyphs with a deterministic
shelf allocator.

The row-wrap and page-allocation thresholds of the retail client are not known, so OpenConquer
does not claim that its atlas coordinates match the retail client's.

Each page exposes its normalized grayscale coverage and a revision number that only increases.

GFX-TEXT-004 mirrors each CPU page into an OpenGL `R8` texture, which is created the first time it
is needed. The GPU copy remembers the last revision it uploaded. When the CPU page's revision
changes, it uploads the whole page again with `TexSubImage2D`.

Texture settings:

```text
internal format = R8
min filter      = nearest
mag filter      = nearest
wrap S/T        = clamp-to-edge
base level      = 0
max level       = 0
mipmaps         = none
```

Coverage is sampled from the texture's red channel.

## Text Layout

Layout works on the tokens from `EncodedTextReader` and does not parse the encoded bytes again.

The pen starts at:

```text
X = 0
Y = 0
```

### Glyphs

A drawable glyph is placed at:

```text
X = pen X + bearing-left
Y = pen Y + top offset
```

Then the pen moves:

```text
pen X += advance
```

A glyph with zero area moves the pen but adds no drawable item.

A missing glyph moves the pen by the cached primary line-height advance and adds no drawable item.

### Newlines

On a newline:

```text
maximum width = max(maximum width, pen X)
pen X         = 0
pen Y        += nominal height + nominal height / 4
```

### Measurement

The measured size is:

```text
width  = maximum horizontal pen extent
height = accumulated newline advance + nominal height
```

An empty string therefore measures:

```text
width  = 0
height = nominal height
```

All layout arithmetic is checked for integer overflow.

### Data Icons

A recognized data icon adds one data-icon item, in text order, with:

```text
icon index
X
Y
resolved width
```

The horizontal pen advances by the icon's width.

The glyph renderer never tries to draw a data icon as text. Until there is a separate renderer for
icon resources, layouts that contain data icons are rejected before any OpenGL state changes.

## Layout Provenance

An atlas page index only has meaning for the glyph atlas that produced it. Rendering must also use
the same font configuration that set the layout's antialias mode.

`NativeTextLayoutSource` therefore ties each layout to:

```text
authoritative primary font record
authoritative CPU glyph atlas
```

Each `NativeTextLayoutEngine` creates one source object, and every layout from that engine refers
to that same object.

The check compares references. Two sources with equal contents are still different sources:

```text
layout.Source must be the exact source bound to the GPU text resource
```

`NativeTextLayoutSource` only records where a layout came from. It does not own or dispose the font
record, rasterizer, glyph cache or atlas it refers to.

`OpenGLTextResource` owns the OpenGL copy of the atlas for one layout source on one OpenGL device.

Before drawing, the renderer checks:

```text
renderer/device identity
text-resource device identity
layout/source identity
layout item kinds
atlas page validity
render-style work budget
```

If the caller gets any of these wrong, the error is raised before the text pipeline changes any
OpenGL state.

## Render Styles

The retail `RENDER_TEXT_STYLE` values are:

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

Eight one-pixel corner passes, then the base text.

Pass order, as confirmed in the retail client:

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

The retail client draws passes at interpolated offsets, starting at the configured offset and
stepping back toward the base position. The base pass comes last. The same offset always gives the
same sequence.

Pass count:

```text
abs(offset X) + abs(offset Y) + 1
```

Example:

```text
offset = (3, 1)

(3, 1)
(2, 1)
(2, 0)
(1, 0)
(0, 0) base
```

Offsets keep their sign.

### PerCornerColor

One base pass, with a separately configured color at each of the four corners.

Corner order in the retail client:

```text
top-left
bottom-left
top-right
bottom-right
```

These four persistent corner colors are not affected by the alpha forcing that applies to the text
and corner colors when antialiasing is disabled.

## Glyph Geometry

Each glyph pass emits six vertices that form two triangles.

Vertex order, as confirmed in the retail client:

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

As one sequence:

```text
TL, BL, TR, BR, TR, BL
```

Vertex colors follow the same order:

```text
TL, BL, TR, BR, TR, BL
```

The shared `TR ↔ BL` diagonal is kept on purpose, because it changes how per-corner colors
interpolate.

Texture coordinates are computed directly from the glyph's atlas region and normalized.

Destination positions are computed with wider integer intermediates before they are converted to
floating-point coordinates.

### Half-Pixel Offset

The retail client's D3D8 code has coordinate details that come from that graphics API.

OpenConquer does not copy the D3D8 half-pixel offset.

The OpenGL path places glyph edges directly in logical pixel coordinates and maps them to clip
space:

```text
xNdc = (x / targetWidth) * 2 - 1
yNdc = 1 - (y / targetHeight) * 2
```

There is no `±0.5` correction.

This gives the same visible logical placement, which the real-driver conformance tests check,
without copying a quirk of the old API.

## OpenGL Text Pipeline

GFX-TEXT-004 adds a separate text pipeline that works with OpenGL 3.3 Core.

Vertex attributes are tightly packed:

```text
offset  0: vec2 position          float
offset  8: vec2 texture coordinate float
offset 16: vec4 color             normalized unsigned byte

stride = 20 bytes
```

Tests check the size and field offsets of the managed vertex struct.

The fragment shader computes:

```text
coverage = texture(atlas, uv).r
output RGB   = vertex RGB
output alpha = vertex alpha * coverage
```

The atlas holds coverage only. Texture RGB does not affect the output RGB channels.

The pipeline sets every draw state it relies on:

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

This is the OpenGL equivalent of the retail client's text render state:

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

Fixed-function state, FVF declarations, COM ownership and the D3D8 pipeline structure are not
reproduced.

After drawing, the text pipeline restores the renderer's defined baseline state. It does not rely
on whatever OpenGL state happened to be set before.

## Text Batching

Reverse engineering shows that the retail client's batched text rendering:

```text
collects vertices by atlas page
flushes atlas-page batches at Font_DrawEnd
```

The order in which the retail client walks the atlas pages during that final flush is not known.
OpenConquer makes no claim to match it.

OpenConquer uses this reproducible order instead:

```text
atlas pages flush in ascending stable page index
```

Within each page:

```text
original glyph order is preserved
render-pass order for each glyph is preserved
```

Page indices follow the order in which atlas pages were created.

Overlap between glyphs on different pages is still an open question. If the retail client flushes
its pages in a different order, overlapping glyphs could be drawn in a different order too.

## Rendering Work Limits

Rendering reuses fixed-size CPU and GPU staging buffers.

Current limits:

```text
staging capacity      = 4,096 glyph passes
maximum draw workload = 65,536 glyph passes
vertices per pass     = 6
```

When the staging buffer is full, it is flushed and the same storage is reused. This does not limit
the length of the text.

The workload cap is an OpenConquer safety limit and is not claimed to exist in the retail client.
It stops extreme trail offsets or untrusted text from causing unlimited CPU work.

For example, the nine-pass outline style can draw

```text
floor(65,536 / 9) = 7,281 glyphs
```

in one render call before it hits the cap.

Once warmed up, staging and batching reuse storage they have already allocated. Managed allocation
can happen when a capacity or an atlas resource grows for the first time.

## OpenGL Resource Ownership

Each object owns a distinct set of resources:

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

GPU objects must be disposed while their OpenGL context is still valid.

As with the existing OpenGL texture resources, the device is checked on use, so a resource created
by one graphics device cannot be used with another.

Renderer construction and disposal follow a fixed ownership order, and resources are released in
reverse. If cleanup hits more than one error, the first one is reported.

## Failure Atomicity

All checks on a draw request run before the first change to text draw state.

They cover:

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

A layout with nothing to draw does nothing at all and does not activate the text pipeline.

Once GPU work has started, a driver or backend error can still happen partway through. The renderer
then cleans up as far as it can and reports the first error. A later exception during cleanup does
not hide it.

## Managed Ownership

### `OpenConquer.Content`

```text
GameFontConfiguration
    ini/Font.ini
    face token
    nominal pixel height

ClientFontSettingsConfiguration
    optional ini/FontSetting.ini
    GUI/chat face settings
    render style
    antialias policy
    shadow/corner colors
    native corner offset

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

OpenGLTextContext
    production ownership façade
    host font discovery
    FreeType library
    rasterizer / face
    layout engine / glyph cache
    CPU atlas
    OpenGL atlas resource

OpenGLTextLayout
    opaque caller-facing measured layout
```

Content and Rendering are independent sibling projects.

The host font APIs are used to find rendering resources, so they live in Rendering. Using them does
not make Rendering depend on Platform.

The Client composition root passes the font and code-page configuration from Content to Rendering
for the selected-skill cooldown text (GFX-UI-007). Rendering still owns host font discovery,
FreeType objects, glyph and layout state, and the OpenGL atlas resources.

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

Work deferred from GFX-TEXT-002 is implemented in the later stages listed below.

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

The rendering work that builds on GFX-TEXT-003 is implemented in GFX-TEXT-004.

Not implemented yet:

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

What is known about page batching:

```text
native batches by atlas page: verified
exact native final page traversal order: unresolved
OpenConquer ascending page-index order: deterministic modern policy
```

GFX-TEXT-004 on its own did not add any HUD or UI text. The first HUD text in production comes from
GFX-UI-007: the selected-skill cooldown text, drawn through `OpenGLTextContext`.

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
GFX-UI-007
production OpenGLTextContext façade
+ selected-skill cooldown text
        ↓
GFX-UI-008
category-8 main-HUD status hints
        ↓
category-9 Magic hints
learned/zero-magic text, native wrapping, colors and 12-pixel font
```

Selected-skill cooldown text was the first UI text in production. GFX-UI-008 adds the next one, the
category-8 main-HUD status-hint panel. It uses the retail StrRes.ini bytes, the normal GUI font
height and the existing OpenGLTextContext. Category-9 Magic hints also use retail magic and effect
metadata, subprofession requirements, configured keyed localization and runtime snapshots.
Real-driver HUD conformance tests cover the retail byte-based wrapping and colored text at 800x600
and 1024x768. Live producers in Gameplay are not connected yet. Separate control tooltips and the
other status-hint categories are deferred.

## Conformance Testing

Unit tests cover the policy logic and the CPU-side behavior.

The host font adapters can only run on their own operating systems. CI tests the native host font
and FreeType interop code on the supported Windows, macOS and Linux environments.

GFX-TEXT-004 adds OpenGL conformance tests that run on real drivers. They use synthetic glyph
coverage with fixed values instead of output from host-selected fonts.

The text conformance tests check:

```text
logical pixel placement
nearest coverage sampling
coverage × diffuse alpha
SrcAlpha / OneMinusSrcAlpha blending
native per-corner TR-BL triangle diagonal
CPU atlas revision → existing GPU texture synchronization
```

The original synthetic GFX-TEXT-004 fixture fills `GlyphAtlas` directly. It does not call FreeType
or use host font discovery.

GFX-UI-007 adds a second real-driver test that goes through the production `OpenGLTextContext`
façade with an injected synthetic rasterizer. It checks the façade's ownership, layout, on-demand
atlas page creation and upload, and GPU drawing, without the pixel variation that host fonts
would bring.

The selected-skill conformance test then combines the synthetic cooldown glyphs with the retail
selected-skill image and cover drawing, and checks the retail client's stateful cover ordering.

The tests keep host fonts out on purpose. The FreeType version and the font file versions used by
the retail client are not known. OpenConquer therefore does not claim pixel-identical glyph bitmaps
just because both clients rasterize glyphs the way FreeType does.

Rasterized glyph pixels can vary with:

```text
font revision
FreeType revision
hinting behavior
host font selection
```

The GFX-TEXT-004 conformance tests instead check rendering from the point where coverage is in the
OpenConquer glyph atlas. From there on, the output is reproducible.

## Modernization Rules

5517 behavior that OpenConquer keeps:

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

Rules for the modern implementation:

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

The general rule is to keep observable behavior and modernize the implementation:

```text
preserve behavior that affects visible output, content compatibility, protocol semantics,
or deterministic gameplay/client behavior

modernize obsolete API representation and incidental native implementation machinery
when doing so does not change the verified observable contract
```
