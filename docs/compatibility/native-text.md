# Native Text Compatibility

Current compatibility contract for reconstructing Conquer Online 5517 text input behavior. Detailed
native addresses, decompilation traces, caller inventories, and unresolved reverse-engineering
evidence belong in the native analysis notes.

## Startup Configuration

### Game Font

Retail `ini/Font.ini` configures the initial game-font request.

The verified format is:

```text
<face token><space><nominal pixel height>
```

Parsing uses the final ASCII space as the delimiter, allowing face names that contain spaces.

The clean 5517 client contains:

```text
Arial 12
```

The compatibility contract is:

```text
face token            = bytes before final ASCII space
nominal pixel height  = integer suffix
invalid/zero height   = 12
```

The face token is preserved exactly at the Content boundary.

Native `$*.tt?` tokens are expanded beneath the Windows Fonts directory during font resolution. That
Windows-specific behavior is not part of configuration parsing and remains deferred to the
font-resolution/rasterizer boundary.

`Font.ini` is a loose-file dependency in the native client. OpenConquer therefore does not resolve
it through WDF packages.

OpenConquer bounds the file to 256 bytes and rejects larger inputs rather than reproducing native
fixed-buffer truncation behavior. This is an intentional defensive modernization outside the
verified retail content path.

Production parser support does not add `Font.ini` to the managed runtime content closure until an
implemented runtime text consumer requires it.

### Normal UI Font Height

Retail `ini/info.ini` contains the normal UI font height:

```ini
[FontSize]
Size=12
Width=6
```

The currently verified text consumers require `Size`.

The compatibility contract is:

```text
[FontSize] Size present and integer → configured value
missing file                       → 14
missing section/key                → 14
invalid integer                    → 14
```

The clean 5517 client resolves the normal UI font height to `12`.

`Width` is not currently exposed because no implemented or verified consumer requires it.

The normal UI font height is distinct from the nominal height in `Font.ini`; their fallback values
must not be collapsed:

```text
Font.ini nominal-height fallback = 12
info.ini normal-height fallback  = 14
```

`info.ini` remains a loose-file configuration source.

### Client Code Page

Retail optionally reads `ini/CodePage.ini`.

Its first line supplies the native client code-page value. Later lines participate in native
font-size-level configuration and are outside the current text-input slice.

The native code-page global initializes to:

```text
0
```

When `CodePage.ini` is absent, the native client leaves this value unchanged. On Windows, `0` means
`CP_ACP`, so the effective encoding depends on the host ANSI code page.

The clean 5517 asset set contains no `CodePage.ini`.

Independent decoding of the shipped retail data corpus establishes CP936 as the authored encoding.
OpenConquer therefore separates the native configured value from its deterministic cross-platform
resolution:

```text
CodePage.ini absent

ConfiguredCodePage = 0
EffectiveCodePage  = 936
```

An explicitly configured nonzero code page is preserved:

```text
CodePage.ini: 950

ConfiguredCodePage = 950
EffectiveCodePage  = 950
```

The first-line parser preserves native-style integer-prefix behavior:

```text
leading ASCII whitespace allowed
optional + / -
consume decimal digits until first non-digit
no digits → 0
```

OpenConquer rejects values outside the signed 32-bit range rather than reproducing undefined native
`atoi` overflow behavior.

`CodePage.ini` is optional and loose-only. Packaged content does not satisfy it.

OpenConquer additionally applies a 256-byte safety ceiling to the first line.

## Encoded Text Model

The native text pipeline operates on encoded bytes rather than Unicode code points.

OpenConquer preserves that boundary before rasterization:

```text
encoded bytes
    ↓
code-page-aware byte walk
    ↓
glyph / newline / data-icon semantic units
    ↓
future measurement and rasterization
```

No UTF-16 `char`, Unicode `Rune`, font rasterizer, glyph atlas, or OpenGL state participates in this
stage.

### String Termination

Text follows native C-string semantics.

The first NUL byte terminates the input:

```text
41 00 42
↓
glyph 0x0041
end
```

Bytes after the first NUL are not inspected.

### Single-Byte Glyphs

A byte that is not consumed as a DBCS lead byte maps directly to the native glyph-cache key:

```text
41
↓
0x0041
```

### DBCS Glyphs

Lead-byte classification follows the Windows DBCS code-page contract.

Supported lead-byte domains are:

| Code page | Lead-byte ranges          |
| --------: | ------------------------- |
|       932 | `81-9F`, `E0-FC`          |
|       936 | `81-FE`                   |
|       949 | `81-FE`                   |
|       950 | `81-FE`                   |
|      1361 | `84-D3`, `D8-DE`, `E0-F9` |

Other code pages produce no DBCS lead bytes at this boundary.

A lead byte with another byte available consumes the pair. The native glyph-cache key is:

```text
(lead << 8) | trail
```

For the verified CP936 character `白`:

```text
B0 D7
↓
0xB0D7
```

Lead-byte classification does not validate the second byte independently. This preserves the native
byte-walking contract.

A lead byte at the end of the terminated input remains a single-byte glyph:

```text
B0
↓
0x00B0
```

A NUL immediately following a lead byte also makes the lead byte dangling because the NUL is outside
the logical text:

```text
B0 00 D7
↓
0x00B0
end
```

### Newline

ASCII line feed is represented separately from glyph data:

```text
0A
↓
newline
```

Layout consequences such as resetting X and applying the native line advance belong to the later
measurement/layout slice.

## Data Icons

The native measurement path optionally recognizes inline data-icon escapes.

Recognition is enabled only for a caller that supplies data-icon behavior. Ordinary text traversal
does not interpret the sequence specially.

A valid token is exactly:

```text
#NN
```

where both `N` bytes are ASCII decimal digits.

Examples:

```text
#00 → icon 0
#07 → icon 7
#42 → icon 42
#99 → icon 99
```

Recognition consumes exactly three bytes.

The token remains valid when its second digit is the final byte of the string.

Malformed or truncated sequences are ordinary glyph bytes:

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

The later measurement layer owns icon lookup, caller-supplied width overrides, and the native
16-pixel fallback when lookup fails or reports zero width.

## Current Managed Boundary

`OpenConquer.Content` owns retail configuration parsing:

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

`OpenConquer.Rendering` owns rendering-facing encoded text semantics:

```text
DbcsLeadByteClassifier
EncodedTextToken
EncodedTextReader
```

Content and Rendering remain independent sibling projects.

The Client composition root will eventually supply Content-derived configuration values to Rendering
when a runtime text consumer exists.

## Current Scope

Implemented and unit verified:

```text
retail Font.ini input contract
normal UI font-height input contract
optional CodePage.ini first-line contract
native configured code-page preservation
deterministic CP_ACP → CP936 resolution for clean 5517
Windows DBCS lead-byte classification
single-byte glyph keys
two-byte DBCS glyph keys
NUL termination
newline recognition
dangling lead-byte behavior
conditional #NN recognition
data-icon indices 00-99
```

Not implemented in this slice:

```text
$*.tt? Windows-font expansion
system font discovery
native font fallback chain
CodePage.ini font-size levels
Unicode decoding for rasterization
font rasterizer
glyph metrics
glyph cache
glyph atlas
text measurement/layout
newline layout advance
missing-glyph fallback
data-icon resource lookup
data-icon width resolution
font batching
antialias behavior
text color/corner behavior
OpenGL text drawing
runtime HUD/text consumers
```

These are later text-rendering slices rather than missing work in the text-input contract.

## Planned Text Reconstruction

The remaining text path is intentionally staged:

```text
GFX-TEXT-001
retail text configuration
+ encoded-byte semantics
        ↓
GFX-TEXT-002
font resolution
+ rasterizer / glyph-metrics seam
        ↓
GFX-TEXT-003
native measurement/layout
+ glyph cache / atlas
        ↓
GFX-TEXT-004
OpenGL text rendering
+ real-driver conformance
        ↓
GFX-UI-001
first native UI consumer
```

The first intended UI consumer is the verified status-hint panel, which combines text measurement
with existing ANI and sprite source-region/stretch behavior.

## Conformance Boundary

GFX-TEXT-001 is deterministic and driver-independent, so unit tests are the appropriate conformance
mechanism.

Pixel-level text conformance is deliberately deferred.

The native rasterizer is FreeType-shaped, but its exact retail version has not been established.
OpenConquer must therefore keep rasterizer output behind an explicit conformance seam and must not
claim native pixel identity from self-generated host-font golden images.

## Modernization Boundary

Preserve observable 5517 text behavior while replacing host-specific or unsafe machinery:

```text
preserve encoded-byte traversal before rasterization
preserve native one/two-byte glyph-cache keys
preserve optional #NN recognition semantics
preserve native configured code-page value
pin unrepresentable CP_ACP default to evidence-backed CP936
keep configuration parsing in Content
keep text/render behavior in Rendering
keep Content and Rendering independent
bound configuration inputs defensively
reject integer overflow deterministically
do not make host ANSI settings part of game behavior
do not expand native Windows font paths during Content parsing
do not introduce font/rasterizer/atlas abstractions before their slice
do not expand runtime content closure without an implemented consumer
```
