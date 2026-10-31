# WORLD-008: Offline map-puzzle preflight

The Content Tool has a read-only command for validating retail puzzle artwork
by **MapDataId**, which is distinct from the server's world MapId:

```bash
dotnet run --project tools/OpenConquer.Content.Tool -c Release -- \
  validate-map-puzzles --source "/absolute/path/to/extracted/5517-client" --map-data-id 1010
```

Run this from the client repository root against a complete, unmodified retail
installation, including the WDF files declared in `ini/package.ini`.
The command does **not** accept a 7z file in place of the installation root.

The tool loads `GameMap.dat`, the selected DMap archive, its ground puzzle
bitmap and its positioned type-8 puzzle bitmaps. It resolves occupied tile
indices through their authored ANI sections and decodes each distinct referenced
DDS/TGA frame using the existing `LooseThenPackage` lookup. It reports the
number of puzzle bitmaps, tile animations and decoded source frames. Missing
or malformed dependencies cause a nonzero exit without changing the source.

The verified retail 5517 source corpus contains 203 puzzle files; its largest
is 95,830 bytes. The existing 128 KiB resource-read bound intentionally covers
the known retail source set. The standalone puzzle parser supports larger
hypothetical inputs, which this preflight does not promise to accept.

## Test and source consistency boundaries

The automated integration tests use unmodified Newbie Village DMap, puzzle
and ANI fixtures. Their tiny DXT1 images are generated test data, **not**
original retail image bytes. A successful operator preflight against the
complete retail installation is required to validate actual DDS/WDF content.

Use a stable retail source directory, not one being installed, patched or
modified concurrently. The command does not take a filesystem snapshot:
like the existing content importer, it assumes operator-controlled input
throughout a run. Do not interpret a run against changing files as verification
of an atomic revision.

This command checks only ground and positioned puzzle artwork. It does not
validate all animated overlays, 3D scenery, hero meshes or GPU rendering. It
does not import or publish assets, modify the runtime content closure, or
activate world entry. When a real renderer consumes these resources, update
`ClientContentClosure`, the tracked manifest and the payload together through
the established **local** import and verification workflow.
