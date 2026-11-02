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

The media pipeline does not yet render the loading screen. Authentication status, loading timeline playback, render-thread ownership, and world admission must be completed and tested before the PR is ready to merge.
