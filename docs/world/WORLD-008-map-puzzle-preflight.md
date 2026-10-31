# WORLD-008: Offline map-puzzle preflight

The Content Tool validates retail map puzzle frames by MapDataId. It uses the
existing map catalog, DMap loader, puzzle/ANI resolver and image decoder, with
loose-first package lookup. Each distinct referenced frame is decoded once.
Missing or malformed content causes failure without writing a content set.

The clean retail 5517 corpus has 203 puzzle files; the largest is 95,830
bytes. The existing 128 KiB resource limit is intentionally stricter than
the standalone parser's general upper bound. The preflight targets the
verified retail corpus, not speculative larger formats.

Operate on a quiescent, stable retail source directory. This check does not
provide a transactional snapshot of files concurrently modified by another
process, and it does not change the trusted content import process.

This validates only ground and type-8 positioned puzzle layers. It does not
activate live rendering, import assets, alter the shipped content closure,
validate 3D scenery or transition the authenticated world session.
