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

The login and character-creation stages retain their fixed 4:3 presentation. The
world uses an 800×600 logical render target; framebuffer resizing changes only its
uniform presentation scale and centered margins. The camera, parallax positions,
visible puzzle tile range, and terrain clipping stay in the same logical coordinates.
Resizing updates the existing render target's destination viewport rather than
recreating OpenGL resources. Expanding the logical world beyond the source-backed
puzzle coverage needs separate map-camera and background-bound validation.

CPU map and scene content is loaded on a cancellable worker. Texture decoding and
incremental uploads happen on the graphics thread before `OpenGLRenderer.BeginFrame`.
The texture cache is tracked by the graphics-context owner; decoded map and
scene state survive context recreation. After admission, login
and role input are no longer dispatched to their inactive screens.

## Remaining work

The default distributed content set is limited to startup and UI requirements. The
world-map assets are not committed to the repository. The content importer accepts an
explicit map-data ID and derives the required map, puzzle, terrain-scene, animation index,
and frame dependencies from the same loaders used by the client. The default import and
verification commands remain unchanged and continue to match the committed set.

To generate a local content set for Newbie Village, from the client repository root:

```bash
SOURCE="/Volumes/EnderChest/Development/Repos/GameDev/Conquer/client-5517/00_clean/clean/5517"
DEST="/tmp/openconquer-world-1010"

dotnet run --project tools/OpenConquer.Content.Tool -c Release -- \
  validate-map-puzzles --source "$SOURCE" --map-data-id 1010
dotnet run --project tools/OpenConquer.Content.Tool -c Release -- \
  validate-map-terrain-scenes --source "$SOURCE" --map-data-id 1010
dotnet run --project tools/OpenConquer.Content.Tool -c Release -- \
  import-client-content --source "$SOURCE" --destination "$DEST" --map-data-id 1010
dotnet run --project tools/OpenConquer.Content.Tool -c Release -- \
  verify-content-set --content-set "$DEST" --map-data-id 1010
dotnet run --project src/OpenConquer.Client -c Release -- \
  --content-root "$DEST/payload"
```

The importer requires a new destination directory and publishes it only after a complete
import. Delete or choose another destination before repeating the command. The generated
content set stays local; do not commit third-party assets to the public repository.
If map resources are missing or malformed at world admission, the client closes the
transferred game session before restoring login input, then displays a content error.
No world session is retained behind the login screen. A future world packet reader must
own disconnect detection; this slice does not consume or discard gameplay frames.

The server must be running for an authenticated-world smoke test. CI validates the importer
with small generated frame resources and the verified map data fixtures; it does not replace
a live OpenGL smoke test.

Type-4 animated scenery, positioned animated overlays, the local hero, HUD, ongoing
world packets, movement, and remaining scenery kinds are not drawn yet. Unsupported
positioned animation entries are skipped without suppressing supported puzzle layers. Those require a shared world-entity depth
ordering pass. The current frame is an authenticated map presentation, not a playable world.

## Verification

The world tests verify first-frame layer insertion order, the hero-centered projection,
and independent puzzle frame advancement. Content tests verify interval parsing and the
default. Validate the source assets with the map puzzle and terrain-scene content tools
before a local end-to-end smoke test.
