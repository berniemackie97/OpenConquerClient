# WORLD-022: Initial local-character part assembly

The authenticated character-info snapshot supplies the body appearance source
and hair composite. At first world entry, the renderer resolves the base body
and, when the linked hair selector is active, the corresponding `armet` part.
The linked source identifier is constructed from the body source, the hair
composite's low three digits, and the recorded `0x1D0D8` hair family offset.
The part catalog resolves mesh and texture identifiers to their actual C3
resources. The `armet` part follows the body's `v_armet` motion attachment,
with bone zero sampled from the same initial idle frame as the body.

World-entry item snapshots (1008 subtype 1) describe tracked equipment, while
the active-equipment snapshot (1009 subtype 46) selects the item IDs to apply.
A tracked item alone does not activate its visual. The initial primary armor
and headwear sources use the equipped item type's low six digits, normalized
to tens and combined with the body appearance source. Headwear replaces the
linked hair source in the primary `armet` slot. Missing active-equipment IDs
are rejected rather than mapped to unrelated items.

The first world view still does not implement the full role-part system:
additional body and headwear variant layers, garment overrides, weapons,
mounts, head attachment records, texture/material overrides, subsequent
equipment updates, and action-driven motion transitions remain outstanding.
The runtime continues to require the authorized C3 archive and role-part
indexes in its content set. Missing packages excluded from the curated
content set are recorded without startup warnings; invalid archives and
duplicate registrations remain visible as diagnostics.
