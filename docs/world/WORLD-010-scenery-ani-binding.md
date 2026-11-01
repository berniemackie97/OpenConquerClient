# WORLD-010: Retail scenery ANI resource binding

This slice resolves type-4 scenery records in the retail DMap to their authored ANI
sections and validates referenced frames offline. It does not create visual
positions, animation clocks, draw order, visibility rules, or a live world scene.

## Evidence

- The original 5517 \`map/map/newbie.7z\` decodes to \`newbie.DMap\`, whose scene has
  38 scenery records. Eighteen type-4 records reference 16 distinct ANI sections.
- Those records reference \`ani/MapScene.ani\` and names such as
  \`newbie17.tga\`; the original 129,051-byte retail ANI maps that section to
  \`data/map/mapobj/newbie/newbie17.dds\` as its sole frame.
- The fixture \`mapscene-newbie-sections.ani\` contains only the 16 source-extracted
  sections, not the full 1,379-section original ANI. The original Newbie scene
  and DMap fixtures are retained unchanged.
- \`20_notes/Rendering/00_rendering_gap_analysis.md\` §2.2: native
  \`CMapSceneryComponent_PerFrameTick\` (\`0x63537B\`) builds the active render deque
  through self-registration; \`CMapSceneryComponent_Render\` (\`0x6352F4\`) draws
  that deque, **not** the raw loaded scenery list. For subtype 4,
  \`+0x0C/+0x10\` hold tile coordinates and \`+0x34/+0x38\` depth spans.

\`GameMapSceneryAniBindings.Load\` preserves the original file association for
traceability, loads each distinct ANI index once, and fails on missing/empty
referenced sections. It deliberately does not resolve type-1 external
\`.scene\` objects, interpret tile coordinates, or claim source order is
visual order.

## Operator verification

From the client repository root, on the full unmodified retail installation:

\`\`\`bash
dotnet run --project tools/OpenConquer.Content.Tool -c Release -- \
  validate-map-scenery-ani --source "/absolute/path/to/clean/5517" \
  --map-data-id 1010
\`\`\`

This read-only command decodes each unique referenced DDS/TGA frame through
\`LooseThenPackage\`. Automated tests use original retail DMap and selected
ANI section **references**, with separately generated DXT1 images to exercise
the decoder. They do not replace the operator's real-texture verification.

The missing native scene-activation, depth/nesting, animation-clock,
projection and external \`.scene\` contracts remain explicit prerequisites
for live drawing. No shipped content, GameServer, camera, or world-entry
behavior is changed.
