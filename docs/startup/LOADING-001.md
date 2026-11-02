# Loading-screen media preparation

The loading-screen display uses authored artwork and a 100-frame timeline. Rendering behavior, progress stops, and status transitions are tracked separately from packaging.

The preparation tool converts the previously source-verified extracted images into RLE32 TGA, which the client content decoder already supports. It verifies the original client file and the hashes of every extracted source file before writing. It never modifies the client source tree, and does not publish any research-only VM captures.

Run the preparation tool from the client repository root on the current development branch. Keep the original client source outside the repository.

Prepare the media using the extraction already verified locally:

```bash
SOURCE="/path/to/client-5517"
EXTRACTED="$HOME/Downloads/OpenConquer-Loading-394"
PREPARED="$HOME/Downloads/OpenConquer-Loading-Prepared"

python3 tools/development/prepare_loading_media.py \
  --client-root "$SOURCE" \
  --extracted "$EXTRACTED" \
  --destination "$PREPARED"
```

The destination must not exist. The original client files remain read-only.

Import and verify the resulting content set through the normal content pipeline. This example also includes map 1010 for local world admission:

```bash
SET="$HOME/Downloads/OpenConquer-Loading-1010"

dotnet run --project tools/OpenConquer.Content.Tool -c Release -- \
  import-client-content --source "$SOURCE" --destination "$SET" \
  --map-data-id 1010 --loading-media "$PREPARED"

dotnet run --project tools/OpenConquer.Content.Tool -c Release -- \
  verify-content-set --content-set "$SET" --map-data-id 1010
```

Every prepared media file is included in the signed-content input manifest and exact closure. A missing file aborts the import before publication. The preparation output itself is not a product release. For the managed client, import with the Content Tool into a new staging directory under `content/`, verify with `--map-data-id 1010`, then promote the complete verified set (manifest and payload together) to `content/retail-5517` on the working branch. Preserve the old set separately until verification succeeds; do not change individual payload files or manifest hashes by hand.

With that selected content set in the canonical repository location, the Product Tool creates and activates a verified local product:

```bash
dotnet run --project tools/OpenConquer.Product.Tool -c Release -- \
  create-local-product --map-data-id 1010

./artifacts/local-product/product/OpenConquer.Launcher
```

The local Product Tool verifies the selected map closure before and after publishing.

## Runtime playback

When the verified loading media is present, the client reads the 100-frame timeline and loads its prepared TGA artwork on the login OpenGL context. The bar and head positions come from the timeline's frame placements. The parent timeline starts authentication at frame 17, holds until the account is ready, and waits at frame 36 for world preparation to complete, and the world presentation starts only after frame 99. Nested head and title animations continue while the parent is held. Starting the sequence stops credential editing without erasing the submitted password. At frame 17, authentication takes the credentials from the editor, clears the editor secret and uses independently owned buffers that are zeroed after the exchange. Focus loss or Escape before frame 17 cancels the pending command and clears the password. The status trace follows account authentication, game-server connection, character initialization and map preparation. The transient starting notification is overwritten before the next presented frame; the map trace remains visible through the closing animation. These transitions use the imported string resources without imposing artificial delays. Failed login attempts reset the overlay. Content sets without the optional media retain their previous startup behavior.

The admitted world is prepared on a cancellable worker using independent read streams. Completion and any failure return to the window thread. OpenGL textures are uploaded incrementally on that thread while the loading timeline continues; world presentation waits until its visible resources are ready and the timeline reaches the close frame. Shutdown cancels and joins the map worker before releasing the authenticated connection. The local authenticated map-1010 smoke test must verify animation continuity, cancellation and the transition before merging.
