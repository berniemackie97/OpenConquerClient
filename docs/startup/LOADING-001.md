# Loading-screen media preparation

The loading-screen display uses authored artwork and a 100-frame timeline. Rendering behavior, progress stops, and status transitions are tracked separately from packaging.

The preparation tool converts the previously source-verified extracted images into RLE32 TGA, which the client content decoder already supports. It verifies the original client file and the hashes of every extracted source file before writing. It never modifies the client source tree, and does not publish any research-only VM captures.

From the client repository root, create a separate worktree if the current checkout has other changes:

```bash
git fetch origin bernie/login-loading-presentation
git worktree add --detach "$HOME/Downloads/OpenConquerClient-loading" origin/bernie/login-loading-presentation
cd "$HOME/Downloads/OpenConquerClient-loading"
```

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

Every prepared media file is included in the signed-content input manifest and exact closure. A missing file aborts the import before publication. The preparation output itself is not a product release; the Product Tool continues to package the verified imported content set using its existing release flow.

## Runtime playback

When the verified loading media is present, the client reads the 100-frame timeline and loads its prepared TGA artwork on the login OpenGL context. The bar and head positions come from the timeline's frame placements. The parent timeline holds at frames 17 and 36 until authentication and world preparation respectively allow it to advance, and the world presentation starts only after frame 99. Nested head and title animations continue while the parent is held. Failed login attempts reset the overlay. Content sets without the optional media retain their previous startup behavior.

This slice does not yet move CPU map decoding out of the window's rendering callback. The map reader's stream ownership and cancellation contract must be audited before asynchronous preparation is introduced. Verify the appearance and transition on a local authenticated map-1010 session before merging.
