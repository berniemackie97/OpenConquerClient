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

CPU map and scene content loads on a cancellable worker. The admitted world
also owns image preparation and the context's texture cache, described below.
Decoded map and scene state survive context recreation. After admission, login
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

## Playable map image preparation

The admitted world owns the map image loader and its OpenGL texture cache.
Each loader batch reads and decodes up to eight ANI images off the frame
thread. A batch stops after its decoded pixels reach 8 MiB and can finish
one larger image within the decoder's existing limits. The context thread
uploads up to four ready textures per frame. It keeps drawing the last
complete map view while the new one is pending, with that view's camera and
role transform. The first map view still gates world presentation. If a
needed image fails to load, the world closes with the content failure reason.

Native resource requests and completion are separate operations (DataThread.dll
`0x1000113A`, GraphicData.dll `0x10003A32`, `0x10003F1C/44`). The playable
client's batch size, upload budget and coherent view are managed policies.
The Studio preview keeps its synchronous preparation contract. Leaving a
world cancels and observes its loader and releases its OpenGL texture cache.

The playable cache targets 512 resident textures and 128 MiB of uploaded
RGBA pixels. It evicts least-recently-used idle textures when either target
is exceeded. The displayed view and the view being prepared both keep their
textures until handoff. A working set larger than the target can finish and
draw, and replacing it makes its unused textures eligible for eviction.
Studio retains its hard preview limits.

An OpenGL pressure test on retail map 1010 compared 24 completed
framebuffers against the synchronous renderer and 99 pending framebuffers
against the preceding view. The tight test cache held at most 24 textures
and 5.81 MiB after each handoff, versus 49 textures and 11.31 MiB in the
unbounded reference. Returning to an evicted view reloaded its art on the
worker. Run the map residency case with the curated payload:

```sh
dotnet run --project tests/Conformance/OpenConquer.Rendering.Conformance -c Release -- --world-map-residency --content-root content/retail-5517/payload
```

## HUD startup preparation

The first world view starts a cancellable job for the HUD's static artwork,
settings and metadata while map textures prepare. The context thread builds
the HUD from the completed CPU content and the loading overlay stays visible
until the first map view, appearance and HUD are ready. Context recreation
can reuse the prepared HUD content. The world cancels or observes its job
when it closes.

The existing HUD layout, input and drawing order carry forward.
The following sections cover host font setup and on-demand HUD artwork.

A local Apple M4 OpenGL probe with the curated 5517 content verified that
76 game-content open attempts took place during worker preparation and that
creating the HUD from the result made no game-content opens. The chrome
and vitals framebuffer hash matched the synchronous baseline. This probe
isolates startup preparation from full frame pacing.

### HUD fonts

The HUD worker discovers the host font catalog once and prepares three
FreeType faces for cooldown text, status hints and magic hints. The context
thread takes those faces when it creates the text atlases. Font selection,
fallback order, pixel heights, antialiasing and code page carry forward.
Each face is transferred once. A canceled or partially attached HUD
releases the faces it still owns. Context recreation reuses static HUD
content and prepares fresh faces on the worker.

The worker completes before the context uses its faces. The existing
library lease keeps each mapped face alive and serializes creation and
destruction on the library, following the
[FreeType face contract](https://freetype.org/freetype2/docs/reference/ft2-face_creation.html).

A local Apple M4 OpenGL probe compared the configured cooldown, status
and magic text rendered through synchronous and prepared faces. All
six framebuffer comparisons matched. Repeat attachment of the entire HUD
took 0.76–0.90 ms after preparation. This probe isolates HUD attachment
from full world frame pacing.

### Selected skill images

The selected skill frame and its independent cover resolve on a world-owned
worker when requested. The frame thread uploads completed images before
drawing. A pending or missing selection draws no selected image. A completed
request for an older selection is discarded. The independent cover can draw
without the selected image, including when the selection is cleared.

The existing frame index, source rectangles, lookup rules and draw order
carry forward. See `20_notes/Rendering/20_main_hud_selected_skill_native_contract.md`
for the native draw at `0x5A2961` and the independent Control.ani Image0
cover. The worker and pending display are managed scheduling choices.

A local OpenGL probe using retail Magic0 and Image0 observed zero opens on
the context thread and matched six synchronous framebuffer comparisons.
The probe drives the selected skill state directly.

### Quickbar artwork

The world owns one quickbar image worker. It reads and decodes up to eight
distinct requests per batch, stopping after the decoded output reaches
8 MiB. A single decode can exceed that threshold within the existing decoder
limits. The context thread uploads up to four ready images and one glow set
per draw. An upload deferred by the budget retains its pixels while the
current slot still needs them. Pending artwork is absent from the draw and
only a completed lookup can cache a missing image. Quantity and addition
overlays wait for their required lookups before drawing, so pending digits
cannot display a partial number.

Item fallback, source rectangles, overlay order and the glow clock carry
forward. Glow time starts at its first draw. The native rules are recorded
in `20_notes/Rendering/19_main_hud_quickbar_native_contract.md` §6.
Graphics recreation retains the CPU worker. World close cancels queued
reads and waits for any active read. A current artwork failure uses the
world content failure path before the next draw.

A local Apple M4 OpenGL probe used the curated 5517 artwork with action,
magic, item fallback, quantity, addition, cover and glow. All six ready
framebuffers matched the synchronous baseline. The worker made 21 content
opens per repeat and the context thread made zero. This probe drives slot
state directly and measures the quickbar draw in isolation.

## World frame timing

The appearance worker reuses its successfully loaded C3 catalog for the
life of the world. Equipment changes still decode the requested parts on
the worker and install only the current result on the context thread.

A 420-frame desktop trace used the production admitted world, curated
retail map 1010, 40 camera corrections and 20 equipment changes. Its
content source rejected context-thread opens. C3 catalog opens dropped from
42 to 2. After warmup the final run spent 5.75 ms median and 9.11 ms p95
in the world callback, including drawing and presentation submission.
Equipment-change windows took 9.90 ms p95 and 13.84 ms maximum. The first
world draw still took 44.69 ms. The trace injects inbound packets after
admission and measures one Apple M4 OpenGL host. It excludes authentication
and compositor presentation time.
