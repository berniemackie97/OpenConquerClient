# STARTUP-001D — independent login chrome layers

Base: `2467f5be0eddcf83c138cfcdc7f3072c578e3a0f` (PR #84 merged).

## Scope

Adds an independently drawn top-dragon ornament and the two unpopulated region/server selection frames. The account panel and parchment remain unchanged. These are **presentation-only frames**, not an authoritative server list or functional buttons. Login, registration, server catalog, credentials, and world entry remain unimplemented.

## Evidence / placement

| Resource | Retail origin | Source symbol | Native/reference anchor | Dimensions |
| --- | --- | --- | --- | --- |
| Top-dragon | `data/main/start.dat` | `start/symbol-0397.png` | `ini/flashLogin.ini [topDragon] (267,26)` | 494×140 |
| Selection frame | `data/main/flash/Group1.swf` | `Group1/symbol-0007.png` | Region `(150,381)`, server `(685,373)` from native startup notes | 158×58 |

`Group1` frame symbol and correspondence to group/server slots are a **first-party placement interpretation**, not asserted as a proven retail timeline mapping. These empty frames do not imply selectable or online servers. Do not introduce placeholder server data.

The exact-retail content manifest remains unchanged. Images were extracted offline, losslessly converted into straight RGBA byte streams, Brotli-compressed and embedded as first-party derived rendering resources; raw Flash files are not shipped or parsed at runtime. Each embedded resource has a fixed decoded dimension and bounded decoded length. The existing 1024×768 background remains embedded separately.

## Resource audit

- Texture lifetime is scoped to the OpenGL context. Constructor failure disposes successfully created textures in reverse order.
- Reuses one server-frame texture for both instances, avoiding duplicate GPU memory.
- No per-frame asset decoding, texture allocation, or text work.
- Coordinates are scaled deterministically from the 1024×768 stage with integer rounding; logical rendering requires 4:3.
- These visuals do not change the world/HUD gate or add input routing.

## Verification

Verify both 1024×768 and 800×600 logical sizes; resize the host framebuffer and inspect alpha blending, art proportions, region/server alignment, and the preserved account panel. Run all normal restore, format, build, test, publish, content-closure and real-driver conformance gates. Do not claim authentic visual-timeline fidelity for offline-extracted static layers.

## Provenance

- `start/symbol-0397.png`: 494×140, source PNG SHA-256 `e3159ca601fddb006350bb9e6d2c4ad5604a98795669b3b96afc6bfc32c3df9c`, extracted RGBA SHA-256 `b1e80e39a4a91d952510e826c9e8c0f0e0f83a353c6f546f322c970f3875d6b1`; embedded Brotli bytes 74769.
- `Group1/symbol-0007.png`: 158×58, source PNG SHA-256 `bdf7c01fa38c527da9a4915f8655ed8a0ee587dfaecd418ef1259b64c1c351dd`, extracted RGBA SHA-256 `b748e63779e1d35283bbc465a2ee111537dacb0a991f345fbb7bc144370cb48c`; embedded Brotli bytes 3456.
