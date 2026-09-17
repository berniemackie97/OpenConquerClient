# Native Text Compatibility

Compatibility contract for Conquer Online 5517 text configuration, encoded-text semantics, font
resolution, glyph rasterization, caching, atlas storage, and text layout.

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
positive caller width
provider width
16-pixel fallback
```

A missing or zero provider width uses the 16-pixel fallback.

OpenConquer rejects a negative provider width as an invalid managed provider result.

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

Rendering color, fragment alpha, GPU upload, and GPU drawing remain later concerns.

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

Each page exposes normalized grayscale coverage and a monotonically increasing revision for later
GPU synchronization.

GPU texture ownership remains outside GFX-TEXT-003.

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

Actual icon resource selection and drawing remain rendering concerns.

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
NativeTextLayout
NativeTextLayoutEngine
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
explicit data-icon width override
provider data-icon width lookup
16-pixel missing/zero width fallback

checked layout and atlas arithmetic
```

Deferred:

```text
CodePage.ini font-size levels

font batching
text color/corner behavior
OpenGL atlas upload
OpenGL text drawing
pixel-level real-driver conformance
runtime HUD/text consumers
```

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

GFX-TEXT-001 through deterministic GFX-TEXT-003 policy are covered primarily by unit tests.

Host-font adapters require execution on their real operating systems. CI therefore validates the
native host and FreeType seams on supported Windows, macOS, and Linux environments.

Pixel-level text conformance remains deferred.

The exact FreeType revision used by the retail client has not been established. OpenConquer
therefore does not claim pixel-identical rendering merely because both implementations are
FreeType-shaped.

Exact pixels can vary with:

```text
font revision
FreeType revision
hinting behavior
host font selection
final rendering state
```

Real rendering conformance belongs to GFX-TEXT-004.

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
512x512 glyph atlas
2-pixel atlas separation
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
do not put GPU objects into measurement/cache/atlas policy
do not claim undocumented atlas coordinates as native behavior
do not claim pixel identity without evidence
```
