# WORLD-014: Authenticated map presentation

The world-entry snapshot supplies the map-data ID and the hero's initial tile position.
After admission, the client loads the matching map, its puzzle resources, and its terrain
scene resources from the configured content root. The role-creation screen transfers its
connection to the application and closes without disposing the transferred game session.

The world scene projects the hero tile through the existing camera and draws positioned
puzzle records, the ground puzzle, and active scenery in component order. Terrain objects
and map-scene animated sprites share the depth-insertion pass.
Each puzzle bitmap advances its own frame clock using
`ini/info.ini` `[PuzzleBmp] FrameIntervalMilliSec`, defaulting to 1000 milliseconds.
Terrain objects use their authored frame intervals.

The login and character-creation stages retain their fixed 4:3 presentation. The
world follows the host aspect ratio until its authored ground and background
puzzles or the current OpenGL device limit further expansion. For exceptionally
wide windows, the logical height contracts with the width so scene geometry keeps
its proportions and available puzzle tiles cover the frame. Camera projection,
puzzle visibility and terrain coordinates use the same resulting viewport.

Framebuffers resize the existing presentation immediately. Only a change to
logical world dimensions recreates its render target; the graphics-context-owned
texture cache remains alive. The depth attachment still requires 16-bit precision.
The render target is constrained by the device's reported renderbuffer and texture
size limits before resource allocation.

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

The map's configured animated scenery resolves through the shared ANI loader. The scene
indexes static sprite anchors into 256-pixel buckets, selects visible buckets, checks
both sprite-offset and anchor visibility, and orders those sprites with terrain objects.
Frame dimensions refine the initial visibility margin after texture preparation.
The shared depth pass keeps terrain objects as footprint parents. A sprite activated
after a terrain object is adopted when its tile lies inside that terrain footprint;
adopted sprites render immediately after their parent in their own axis-aware order.
Previously activated sprites stay in the top-level order.

Positioned animated-overlay entries are parsed but not drawn. The verified map
catalog has no such animated children; Newbie Village's positioned overlay is
a puzzle background already drawn in component order. The local hero, HUD,
ongoing world packets, movement, and remaining scenery kinds are not drawn yet.
These must join the existing scene pass rather than introduce separate drawing
systems. The current frame remains an authenticated map presentation, not a
playable world.

## Verification

The world tests verify first-frame layer insertion order, the hero-centered projection,
independent puzzle frame advancement, animation binding, spatial bucket selection,
sprite projection, shared scenery depth insertion, terrain-footprint adoption, and child ordering.
Content tests verify interval parsing and the
default. Validate the source assets with the map puzzle and terrain-scene content tools
before a local end-to-end smoke test.
