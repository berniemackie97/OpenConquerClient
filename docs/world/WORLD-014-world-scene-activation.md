# WORLD-014: Authenticated map presentation

The world-entry snapshot supplies the map-data ID and the hero's initial tile position.
After admission, the client loads the matching map, its puzzle resources, and its terrain
scene resources from the configured content root. The role-creation screen transfers its
connection to the application and closes without disposing the transferred game session.

The world scene projects the hero tile through the existing camera and draws the map's
positioned puzzle records, ground puzzle, and terrain objects in component insertion order.
Each puzzle bitmap advances its own frame clock using
`ini/info.ini` `[PuzzleBmp] FrameIntervalMilliSec`, defaulting to 1000 milliseconds.
Terrain objects use their authored frame intervals.

Image decoding and texture uploads happen before `OpenGLRenderer.BeginFrame`.
The texture cache is tracked by the existing graphics-context owner; the decoded map and
scene state are not OpenGL resources and survive context recreation. After admission, login
and role input are no longer dispatched to their inactive screens.

## Remaining work

The curated published runtime content does not yet include the map 1010 puzzle and scene
assets. A client built only from that content set will report missing world resources after
successful admission. To exercise this slice, run with a complete authorized client content
root; do not treat CI's synthetic fixture as a live graphics test.

Type-4 animated scenery, the local hero, HUD, ongoing world packets, movement, and
remaining scenery kinds are not drawn yet. Those require a shared world-entity depth
ordering pass. The current frame is an authenticated map presentation, not a playable world.

## Verification

The world tests verify first-frame layer insertion order, the hero-centered projection,
and independent puzzle frame advancement. Content tests verify interval parsing and the
default. Validate the source assets with the map puzzle and terrain-scene content tools
before a local end-to-end smoke test.
