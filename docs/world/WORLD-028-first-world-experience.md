# WORLD-028: First-world experience band

The experience band uses the current character level and experience from the
authenticated character snapshot and subsequent 10017 attribute updates.
Its maximum comes from `ini/LevExp.dat`, not from current experience or a
fixed level-independent range.

The content reader decodes the bounded table with seed 0x4D2 and validates
five fields per non-empty record. Key zero contains the player progression
requirements, indexed by ascending one-based level. Other table keys do not
affect the player experience bar. Missing levels, including levels beyond
the supplied player table, have no known maximum and leave the band hidden;
a zero requirement also leaves it hidden.

The level-specific range is resolved every world frame using the existing
level and experience state. The HUD's established scaling and drawing code
owns the band geometry. The progression table is loaded once per authenticated
world and is cleared with that world on disconnect. It is included in the
required content set and cannot be synthesized from the character packet.

This slice does not add experience events, character leveling rules,
skill gauge status transitions, or transformation behavior. These belong
to their corresponding world-state and interaction slices.

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
