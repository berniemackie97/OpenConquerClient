# WORLD-006: Bind puzzle tile indices to ANI frames

## Evidence and scope

The `.pul` puzzle grid, empty sentinel, `Puzzle%d` lookup and frame selection are documented in `20_notes/Rendering/05_dmap_positioned_overlay_system.md` and `Game_Map/world_render_contract_5517.md`. The PUX terrain-cache path does not establish puzzle drawing. Cache reuse and missing-section handling are application decisions.

