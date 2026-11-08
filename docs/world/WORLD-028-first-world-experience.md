# WORLD-028: First-world experience band

## Evidence and scope

The `ini/LevExp.dat` level table, seed and level-band lookup are documented in `20_notes/Entity_System/chero_class.md` and `Rendering/13_hud_progress_gauges.md`. Missing or zero ranges are handled by the application HUD. Loading completes before world hydration.

## World entry catalog preparation

The admitted world starts one background job to read the item type, item
addition and experience tables alongside map preparation. The frame thread
takes both completed catalogs before constructing the hero and replaying the
entry snapshot. The one prefetched live frame stays queued until hydration
finishes. A failed receive or EOF can still close the session during preparation.

A failed required catalog closes the world as content unavailable. On shutdown
the socket can close while an outstanding file read finishes. World cleanup
observes the canceled jobs before releasing ownership.

On Apple M4, 20 repeat loads of the curated 5517 catalogs took 20.0–32.6 ms
on the caller with synchronous loading. With the worker, the longest submit,
poll or take operation per repeat was 0.003–0.020 ms. Worker elapsed time
remained 20.1–32.7 ms. Item, addition and progression lookups matched.
This probe measures the catalog handoff. Full frame pacing and map/HUD image
streaming remain under CP2.5.
