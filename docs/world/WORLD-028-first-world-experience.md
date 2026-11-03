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
