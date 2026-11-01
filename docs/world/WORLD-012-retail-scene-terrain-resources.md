# WORLD-012: Native terrain .scene resource closure

The 5517 DMap's **type-1 scenery records** reference external, uncompressed
\`.scene\` terrain-object lists. These are prerequisites for the native scenery
spatial-index and depth-pass; they are not equivalent to type-4 ANI scenery
records resolved in WORLD-010.

## Verified native contract

\`20_notes/Game_Map/world_render_contract_5517.md\`, §4, anchors
\`CMapTerrainObjectGroup_LoadTerrainObjectListFromPath\` at \`0x63F24D\`.
Each file begins with a 32-bit object count. Each object holds a 0x100-byte
ANI path, 0x40-byte section name, nine little-endian 32-bit fields
(render offset X/Y, interval, grid W/H, authored ARGB, anchor offset X/Y,
draw parameter), then 12 bytes per metadata cell, row-major.

**Evidence correction:** the report's sentence \`object size = 0x144 +
12*w*h\` is inconsistent with its own field table. The correct fixed
length from those verified fields is **0x164 bytes per object**:
\`0x100 + 0x40 + 9*4\`. The original retail 5517 files independently
confirm this: \`stand01.scene\` is 552 bytes with one 4×4 record
(\`4 + 0x164 + 16*12\`), while \`stand06.scene\` is 408 bytes with one
2×2 record (\`4 + 0x164 + 4*12\`). No padding or extra field is invented.

Each metadata cell's original dwords provide, in order, passability,
surface, and elevation delta. Native stores their low 16 bits.
The object tile is DMap placement tile plus the object's anchor offsets;
the object render offset remains a distinct screen-pixel displacement.
The authored color is retained but the draw-time tint is the map world
color, as verified in the native render pass.

## Implementation and checks

\`GameMapTerrainSceneFile\` parses the complete source byte range with
bounded count/cell allocation, exact source consumption, safe ANI paths,
original grid order and all nine authored fields.
\`GameMapTerrainSceneResources\` loads one scene file per distinct DMap
type-1 reference, binds exact ANI section names, and preserves the
full ordered list of map placements. Shared file assets are reused across
placements. It does **not** convert file order to depth order, change
passability, or instantiate a renderer.

In Newbie Village 1010 the original DMap has **20 type-1 placements**
referencing exactly **six** distinct scene files:
\`stand01\`, \`stand02\`, \`stand03\`, \`stand05\`, \`stand06\`,
and \`stand08\`. Their original ANI sections each reference one DDS
frame. Automated tests load the existing original retail DMap fixture
and construct minimal \`.scene\`, ANI and DXT1 contents strictly to
test parsing, resource ownership, missing dependencies and corruption.
No original retail \`.scene\` or image bytes are committed.

To verify all actual retail files and decoded frames, run from the
client repository root:

\`\`\`bash
dotnet run --project tools/OpenConquer.Content.Tool -c Release -- \
  validate-map-terrain-scenes \
  --source "/absolute/path/to/clean/5517" --map-data-id 1010
\`\`\`

This command is read-only. It does not publish assets or activate the
game scene.

## Next native-backed integration boundary

The verified index (\`0x6414C2\`, \`0x641D2C\`) separates fixed
256px-bucket scenery from always-ticked moving types, with strict
X-outer/Y-inner bucket iteration. Terrain objects act as type-2 depth
parents with their own child insertion ordering. Those requirements
must be preserved when the renderer is integrated; neither the raw
\`.scene\` order nor the DMap placement order is a render sort.

No GameServer, live entry, GPU state, or product payload changes.
