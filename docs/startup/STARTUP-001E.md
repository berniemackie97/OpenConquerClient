# STARTUP-001E: Login selection chrome

## Scope

Draws the original region-selection cell skin in a 2x8 grid and the original
server-selection cell skin in a 3x4 grid. Both are derived offline from the retail SWF
artwork. This covers drawing only; the server catalog is separate.

The cells are empty and inert. There are no endpoints, online/offline indicators, region
or server labels, selection states, login action, world-entry permission or network
activity, and an empty cell says nothing about availability. When a live catalog is
connected later, it will supply labels, identity, status and which clicks are allowed,
without changing the cell geometry.

## Provenance

| Resource | Offline source | Size | PNG SHA-256 | Decoded straight RGBA SHA-256 |
| --- | --- | --- | --- | --- |
| `region-cell.rgba.br` | `data/main/flash/Group1.swf` bitmap symbol `2` | 154x54 | `860f7353a21a807bc75e71950cef5f166439a67c095d2b1056a5e02a5b3e57ef` | `45015f25884557fe7a279bc9f84f9632c24fb8db11b0cdbb4f9bf98d9b95559f` |
| `server-cell.rgba.br` | `data/main/flash/Group1.swf` bitmap symbol `7` | 158x58 | `bdf7c01fa38c527da9a4915f8655ed8a0ee587dfaecd418ef1259b64c1c351dd` | `b748e63779e1d35283bbc465a2ee111537dacb0a991f345fbb7bc144370cb48c` |

The original bitmaps were decoded losslessly to straight-alpha RGBA and
Brotli-compressed offline. Only the decoded pixel data is embedded. No Flash runtime and
none of the source SWF files ship with the client. These are derived assets, and the
retail manifest and its content closure are unchanged.

## Layout and lifetime

- Tests check the source images' identities and dimensions against their hashes.
- All cells of one type share a single OpenGL texture. There are no per-cell GPU
  textures.
- Grid coordinates are reconstructed by eye from the supplied 1024x768 reference
  screenshot. They do not claim that `sr` or `ss` are per-cell movie positions.
- The backdrop, header, footer, top dragon and pixel-aligned account panel are
  unchanged. `AccountPanel.Y = 637` still holds.
- `LoginVisualLayout` maps reference placements to 800x600 with integer rounding and
  checks the 4:3 aspect ratio.
- Resources are decoded once at startup and textures are created once per OpenGL
  context. Drawing allocates no managed objects per cell.
- Startup, HUD and world access and input ownership are unchanged.

## Testing

Run locked restore, format verification, Release build and tests, retail content
verification, smoke tests on real drivers at 1024x768 and 800x600, and the resize and GL
disposal checks. Unexpected transparency, clipping overlap, or a moved account, header
or footer layer is a failure.
