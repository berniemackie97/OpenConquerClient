# Hero vitals evidence — CP2.2

`native-stats-5517.json` contains static-disassembly-derived golden cases for the
clean 5517 `Conquer.exe`, SHA-256
`b6ba5ab8573ec073ea271f57b16fa14de1c67bfb44efd5aa7cfbfc0d3b488a62`.
Expected results were calculated independently from the signed/wrapping x86
control flow, before comparison with the managed resolver. These are not captured
native runtime results or framebuffer comparisons.

The fixture records instruction bytes for life/mana, status 33, signed stamina
cap and snapshot clamp. Full disassembly, annotation readbacks and byte verification
are in `20_notes/Rendering/evidence/hero-model-2026-11-06/`; the verifier checks
them against the clean PE. `20_notes/Rendering/hero-model-cp22.md` lists every anchor.
The JSON `common` values describe the default attributes used by the test helper.

The neutral encoded item-type row is the existing pinned original record from
`OpenConquer.Content.Tests/Items/ItemStatCatalogTests.cs` (`FirstEncodedItemTypeRow`,
type 50000, zero stat contributions). It is included to exercise real decoding
when advancing the model. The equipped armor row/packets and experience table in
the composition test are independently encoded synthetic inputs; their 296/-10
contribution and level-1 requirement of 120 are test data, not native captures.

Coverage includes profession branches, overrides and map-bit distinctions,
signed overflow/floors, stamina status-word selection, snapshot signed comparison,
HP-only follower reset, deferred one-shot clamp, equipment updates and fresh-world
state. Snapshot-to-later-life-update ordering in a single ingress batch remains a
CP2.3 gap; this fixture does not close it.
