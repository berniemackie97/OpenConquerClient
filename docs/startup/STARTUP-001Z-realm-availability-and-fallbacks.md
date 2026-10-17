# STARTUP-001Z — Source-faithful realm fallbacks, opening timing, and live status boundary

## Verified retail source facts

- `start.dat` `DefineSprite_180/frame_1/DoAction.as::flush`: a populated server slot with a picture uses `data/main/flash/<pic>`; a populated slot with an empty picture uses `data/main/flash/defaultS.swf`; an unpopulated slot uses `data/main/flash/unopenedS.swf`, with its hit area disabled. These are native fallback frames, not placeholder artwork.
- `flushLight`: status `1..4` selects `DefineSprite_34` frames `1..4`; invalid codes select frame `4`; absent slots hide the light. Source colors are red, yellow, green, purple respectively. The exact operational meaning of each color is **not** established by the reviewed notes, so no occupancy or health assertion is made from the colors alone.
- `CLoginFlashDlg_BuildAndPushServerListToFlash` seeds status code `3` on its first push, before an optional status URL refresh. That historical presentation seed is **not live evidence** and must not be translated into a modern 'online' health claim.
- `loginFlash.swf` sprite 25 contains 24 decoded two-golden-dragon video frames followed by 3 transition frames at 30 fps. Source RGBA is opaque with legitimate dark pixels. The motion begins on first actual presentation, not during OpenGL resource creation. This prevents skipped video due to initialization/render scheduling delays without artificially lengthening its verified timeline.
- The separate startup splash bitmap in `StartupWindowSequence` precedes the login movie. Do not equate a missing splash bitmap with missing `loginFlash.swf` or overlay fake dragon artwork.

## Corrected presentation

- Draw native `Unopened` art over the static realm base for empty slots; draw native `Default` art for populated slots without a trusted icon ID. Source-specific icons still come from the verified icon registry when `IconId` is present.
- Every populated realm name is a label **beneath** the movie thumbnail, including when the trusted directory has no icon ID. Do not paint text across a fallback image. Empty slots get no label and no hit area.
- Suppress synthetic green status lights. Keep original four authored light symbols in the asset catalog for future verified use; do not allocate their GPU sheet until the live status contract is connected.
- The source animation and selection effects, authenticated realm catalog, and disabled authentication transport remain unchanged.

## Required live status architecture — not yet connected

**The realm-directory authority is NOT the realm-health authority.** A signed routing catalog identifies the server and its trusted account endpoint. Presence of that entry does not imply availability, capacity or occupancy. Login availability and server population are different facts. Do not probe account endpoints from the client to infer population.

The producer/consumer contract required before showing a colored light:

1. A trusted service operated by the studio derives status from the **authoritative realm heartbeat, capacity and admission state**. 'Connected to login port' is not equivalent to 'world online and admitting players'. There must be well-defined status semantics and a versioned mapping to the original four authored colors, verified from the 5517 Flash/native contracts.
2. Distribute a **separate, domain-separated, signed** status snapshot over HTTPS. The payload includes product, schema version, short issued/expires times (recommended maximum 45 seconds), a monotonically increasing publisher sequence, and bounded `(regionId,realmId,statusCode)` entries. It must never contain or override account endpoints. Keys/URLs are pinned to explicitly configured trust, never downloaded from server descriptions or untrusted artwork metadata.
3. Parse strictly (unknown/duplicate fields and duplicate server IDs rejected); reject invalid signatures, clock skew beyond policy, stale/expired snapshots, sequence rollback and unknown server identities. Use bounded response sizes and bounded total entries. Carry refresh cancellation through shutdown.
4. Poll with bounded jitter at an interval **shorter than the signed TTL** (for example 15 seconds for 45-second TTL), with one in-flight request, capped exponential backoff, network timeout, ETag support only as an optimization, and no polling on the render thread. An expired or untrusted result must resolve to `Unknown`, not to an old green frame. The render thread consumes an immutable validated snapshot.
5. Binding: only render status for a server present in the *currently authenticated* directory with matching stable IDs. Catalog expiry/replacement revokes status binding immediately. Render index `1..4` only for an authenticated, current status with explicitly verified code semantics; hide indicator when unknown or absent. Never gate credential editing or select a different auth endpoint from status alone.
6. The producer's live data freshness and telemetry provenance must be tested independently (server restart, partition, delayed heartbeat, draining, overload, bad signature, stale cache). UI tests must simulate expiry, rollback, reordering, unavailable publisher, and rapid catalog replacements.

**There is no status publisher in this PR, no configured signed status authority, and no proven enum-to-operational-meaning mapping.** Therefore this correction deliberately renders no live-status color. Implementing a fake signed feed, hardcoded green, or trusting old `ini/common.ini` URL would violate the fail-closed product requirement.

## Acceptance

- Verify resource hashes and source provenance for both `defaultS.swf`/`unopenedS.swf` composed shape-3 exports.
- Run Release build, formatting, full tests, packaged content verification, and Linux/Windows/macOS CI.
- Run both 1024x768 and 800x600; absent realms must show the real empty framed visual, present realms without mapped icon must show the original default thumbnail and lower chrome, and named realm art must remain untouched.
- Observe the two gold dragons during the opening at 30 fps. The screen should not skip the opening because GPU initialization consumed its playback window. Capture a short recording if it is still reported black; distinguish the earlier startup splash window from the login Flash overlay.
- Do not merge STARTUP-001Z until original region/server transitions, the real status producer/consumer integration, loading/error UI, font fidelity and visual regression gates are completed.
