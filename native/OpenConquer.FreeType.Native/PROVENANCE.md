# FreeType Native Provenance

This document defines the reviewed FreeType source baseline used to produce the native runtime
assets for `OpenConquer.FreeType.Native`.

The source under `upstream/freetype/` is vendored source. It is intentionally not a Git submodule
and must not be updated or modified independently of this provenance record.

## Review Status

Review date: 2026-09-16

The reviewed source consists of:

- an immutable upstream FreeType base commit;
- a narrowly scoped set of upstream fixes applied on top of that base;
- no OpenConquer-authored changes to FreeType source;
- no later unreleased FreeType feature additions.

The resulting reviewed source tree is the authoritative source identity.

## Upstream Base

Upstream repository:

`freetype/freetype`

Base commit:

`5a280ecde6f324de0d226261036e736e0cb49a71`

Base commit date:

`2026-05-14`

Base commit subject:

`* src/truetype/ttgxvar.c (TT_Get_Var_Design): Zero extras.`

Base tree:

`a24b67ca7305dfa6036f5342a17c3f441203afc3`

Reviewed OpenConquer source tree:

`f4b976c6f83c26866fa3df99d1f51fe696ca1735`

FreeType's license is retained unmodified at:

`upstream/freetype/LICENSE.TXT`

Binary distributions of `OpenConquer.FreeType.Native` are based in part on the work of the FreeType
Team (<https://freetype.org>).

## Reviewed Upstream Backports

The reviewed tree is constructed by applying the following upstream FreeType commits to the base
commit, in this exact order.

| Upstream commit                            | Area                | Reason                                                                                         |
| ------------------------------------------ | ------------------- | ---------------------------------------------------------------------------------------------- |
| `f9010851e4c8f5589955e70c32c5a8d9508c39e9` | Auto-hinting        | Correct GSUB coverage validation by checking the final element.                                |
| `27229dccf89edabd16f5a13e13a30b486384df7f` | Auto-hinting        | Reject overlapping GSUB coverage ranges.                                                       |
| `b08a2eb0dd37f4a6c886fa5b0ecf5b3e1d27aac7` | Auto-hinting        | Complete the coverage-validation hardening with unsigned values.                               |
| `0d6de69ca85a9b97e933e5c3b1e53b0349026560` | SDF                 | Fix a crafted-font heap use-after-free in `sdf_generate_with_overlaps`.                        |
| `b6c6934a76fae579fce081b03e2e74aca17e7eeb` | SFNT/BDF            | Fix an out-of-bounds read while locating embedded BDF properties.                              |
| `656cb777798fa420a13faba3758779e9ed6c4798` | SFNT/WOFF           | Reject unrealistic WOFF sizes and compression factors.                                         |
| `e8c8c6477c78c6fb22d0022ce9e1febee9860051` | OpenType validation | Correct the OpenType MATH kerning table bounds check.                                          |
| `e86492e2790a542cbea1295823ea0cb0c4927555` | PFR                 | Fix a heap over-read in PFR kerning lookup.                                                    |
| `a3cb5858f9f3e7fa361e2828312d9145d10ac025` | Windows FNT         | Prevent malformed resource-directory entry counts from wrapping and producing an endless loop. |
| `50ef5a72980e3afafa7f84d563e63ef65d3800de` | Auto-hinting        | Prevent pointer wrap-around while validating extension lookups on 32-bit platforms.            |
| `f3ca71c9900fe860849b3163a6e2c1e765b291d9` | CID                 | Limit pathological overlapping CID subroutine dictionaries.                                    |
| `342e5809b1aa8aed02b9be1df0015fa7e1e0e244` | CFF                 | Reject an invalid CFF name index before copying from a null data pointer.                      |

These are upstream FreeType changes. OpenConquer does not maintain independent patches to the
FreeType implementation.

Cherry-picking these commits creates new local commit objects whose commit identifiers can vary
because the committer metadata can change. Those synthetic cherry-pick commit identifiers are not
provenance identifiers.

The authoritative reconstructed-source identity is the resulting Git tree:

`f4b976c6f83c26866fa3df99d1f51fe696ca1735`

## Security Review Boundary

The source baseline was reviewed against publicly available FreeType security information and
upstream fixes through 2026-09-16.

At that review point:

- `CVE-2026-23865`, involving an integer overflow while parsing variable-font item variation stores,
  was fixed upstream before this base.
- `CVE-2026-50811`, involving an out-of-bounds read in `TT_Get_Var_Design`, is fixed by the selected
  base commit itself.
- additional upstream malformed-input and memory-safety fixes discovered after the base commit were
  reviewed for applicability and the applicable fixes listed above were backported.
- fixes affecting code introduced only after the selected base were not backported.

This is a point-in-time source review. It is not a claim that FreeType can never receive a later
security advisory. New upstream security information requires a new review of this baseline.

## Excluded Later Feature Development

OpenConquer intentionally does not track the current FreeType development branch.

In particular, FreeType VARC support was introduced later by upstream commit:

`d939d15557ecd6309d19b6ac18be329adbced45d`

That feature is not part of this reviewed baseline.

The reviewed tree therefore contains neither:

- `src/truetype/ttvarc.c`
- `src/truetype/ttvarc.h`

nor the `TT_CONFIG_OPTION_VARC` configuration option.

This boundary avoids importing unrelated unreleased FreeType 2.15 development, new font-processing
attack surface, and unrelated rendering behavior changes while addressing fixes applicable to the
selected baseline.

## Build Contract

OpenConquer builds FreeType from this vendored source through the wrapper defined by:

- `CMakeLists.txt`
- `CMakePresets.json`

The official native runtime targets are:

- `win-x64`
- `win-arm64`
- `osx-x64`
- `osx-arm64`
- `linux-x64`
- `linux-arm64`

The wrapper rejects unsupported runtime identifiers and mismatches between the declared runtime
identifier and the configured target architecture.

The build deliberately disables optional external dependencies:

- zlib
- bzip2
- libpng
- HarfBuzz
- Brotli
- HVF

The resulting package therefore does not acquire those external native dependencies through the
FreeType build.

The canonical package assets are:

```text
runtimes/win-x64/native/freetype.dll
runtimes/win-arm64/native/freetype.dll
runtimes/osx-x64/native/libfreetype.dylib
runtimes/osx-arm64/native/libfreetype.dylib
runtimes/linux-x64/native/libfreetype.so
runtimes/linux-arm64/native/libfreetype.so
```
