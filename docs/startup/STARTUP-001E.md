# STARTUP-001E — Login selection chrome

## Scope

Render the verified original region-selection cell skin in a bounded 2×8 grid and the
original server-selection cell skin in a 3×4 grid. Both are derived offline from the
retail SWF artwork. This is presentation-only, not the server catalog.

No endpoints, online/offline indicators, region or server labels, selection states,
login action, world-entry permission, or network activity are fabricated. Inert
empty cells convey no availability. Future live catalog binding will supply labels,
identity, status and click authorization without changing their drawing geometry.

## Provenance

| Resource | Offline source | Size | PNG SHA-256 | Decoded straight RGBA SHA-256 |
| --- | --- | --- | --- | --- |
| `region-cell.rgba.br` | `data/main/flash/Group1.swf` bitmap symbol `2` | 154×54 | `860f7353a21a807bc75e71950cef5f166439a67c095d2b1056a5e02a5b3e57ef` | `45015f25884557fe7a279bc9f84f9632c24fb8db11b0cdbb4f9bf98d9b95559f` |
| `server-cell.rgba.br` | `data/main/flash/Group1.swf` bitmap symbol `7` | 158×58 | `bdf7c01fa38c527da9a4915f8655ed8a0ee587dfaecd418ef1259b64c1c351dd` | `b748e63779e1d35283bbc465a2ee111537dacb0a991f345fbb7bc144370cb48c` |

The original binary assets were losslessly decoded into straight-alpha RGBA and Brotli
compressed offline. Only the decoded pixel resources are embedded. Neither a Flash
runtime nor the source SWF files are shipped. These are **derived** assets; the
exact-retail manifest and its content closure are unchanged.

## Layout and lifetime

- Source-image identities and dimensions are verified against hashes in tests.
- One OpenGL texture per visual type is shared by all cells. No per-cell GPU textures.
- Grid coordinates follow the supplied 1024×768 reference screenshot as a **visual
  reconstruction**, not an assertion that `sr` or `ss` are per-cell movie positions.
- The established backdrop, header, footer, top dragon, and pixel-aligned account
  panel are unchanged. `AccountPanel.Y = 637` remains authoritative.
- The existing `LoginVisualLayout` maps reference placements to 800×600 using integer
  rounding and validates the 4:3 contract.
- Resource decoding runs once at startup, texture creation once per OpenGL context,
  and draws allocate no per-cell managed objects.
- No startup/HUD/world access or input ownership changes.

## Acceptance

Run locked restore, format verification, Release build/tests, retail content verification,
real-driver smoke tests at 1024×768 and 800×600, resize and GL disposal checks. Reject
unexpected transparency, clip overlap, or displaced account/header/footer layers.
