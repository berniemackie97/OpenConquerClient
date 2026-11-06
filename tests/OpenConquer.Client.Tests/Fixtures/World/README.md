# Off-map tile evidence — CP2.4c

`off-map-5517.json` contains static-disassembly-derived golden cases, not native
runtime captures. The clean5517 executable is SHA256
`b6ba5ab8573ec073ea271f57b16fa14de1c67bfb44efd5aa7cfbfc0d3b488a62`.
The existing original Newbie map fixture is 132×132; its projection base is
(4224,16). Expected projection values were calculated independently from signed
32-bit SUB/LEA/SHL and division at `0x61D850`, not from the managed projector.

Eleven projection cases include negative tiles, u16 crossings and int32 wrap.
Ten visibility cases trace the inclusive comparisons at `0x61E09A..0x61E0AC`,
margin2 and zoom-scaled margins. Six depth cases trace `0x635AE6` and footprint
adoption `0x644E0E`, including the wrapped sum -2 and wrapped equal-Y overlap
branches. Terrain and sprite placements are synthetic inputs to those proven
branches. Expected cases were not observed in a running native client.

Raw instructions, six annotation readbacks and clean-PE verification are in
`20_notes/Network/evidence/off-map-tiles-2026-11-06/`. The scoped authority and
height/motion limits are in `20_notes/Network/off-map-tiles-cp24c.md`.
