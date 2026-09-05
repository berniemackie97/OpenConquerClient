# Launcher completion and audit record

## Current boundary

The launcher implements the desktop host and diagnostic foundation only. Installation, product
state, settings, login, server discovery, secure client handoff, and final UI are not complete.
Passing the host tests is not evidence that those responsibilities exist.

The next implementation must start from the current code and relevant evidence, then select a
coherent responsibility with observable behavior. Do not add every future state to an enum before
its transitions and owning operations are known.

## Diagnostic repair slice

Branch: `bernie/launcher_diagnostic_bounds`.
Baseline: `dc979ed` (`harden launcher host lifecycle and diagnostics (#15)`).

The adjacent host audit found:

- bounded exception-tree traversal still formatted arbitrarily large stack traces;
- exception type names had no output bound;
- the sink's default destructuring depth silently discarded allowed nested diagnostics;
- fully qualified malformed diagnostic paths could escape storage fallback;
- documentation incorrectly called the file rolling threshold a strict maximum;
- the proposed replacement authentication architecture conflicted with native-parity reconstruction.

The repair caps type-name output at 512 UTF-16 code units, stack output at 32 frames and 4,096 code
units, and retains the existing eight nested levels / sixteen-object traversal budget. Separate flags
identify omitted names, frames/text, and children. Truncation preserves surrogate pairs. Stack output
uses only declaring-type and method metadata; virtual exception text, remote stack text, signatures,
source paths, and application values are excluded. CLR stack capture and reflection allocations are
outside the formatting bound; resource exhaustion cannot be made recoverable by diagnostics.

Child lists are created only for populated nodes and exposed through read-only wrappers. The sink
preserves the full permitted tree. Malformed-path fallback remains confined to storage setup;
programming errors in logger configuration are not swallowed by an added general catch.

The host composition roots, exception-observer teardown ordering, standard-user Windows policy,
dependency isolation, and publish isolation are preserved. No packet, credential transformation,
server code, runtime content, or gameplay behavior changes in this slice.

Meaningful regression coverage includes deep recursive stacks, oversized runtime method/type names,
surrogate boundaries, hostile exception text overrides, injected remote stack text, nested and
aggregate budgets, malformed absolute paths, and the actual persisted JSON at the deepest allowed
level. The persisted-depth regression failed against the original sink configuration.

## Slice validation

Verified locally on macOS arm64 with the pinned .NET 10.0.400 SDK:

- locked solution restore;
- `dotnet format --verify-no-changes --no-restore`;
- Release solution build with zero warnings and errors;
- all 377 solution tests, including 47 launcher tests, passing with no skips;
- tracked and published content-set verification: five files, 1,142,443 bytes each;
- separate Release launcher and client publishes;
- `Server.dat` absent from both complete publish trees;
- launcher/game dependency and content isolation in both directions;
- `git diff --check`, targeted diagnostic/security searches, and full changed-code/document review.

No unresolved finding remains in this repair slice. Windows/Linux runtime checks were not run
locally; existing cross-platform CI remains required when the user submits the change. No GUI
behavior changed, and no Avalonia headless test dependency was added. The launcher as a whole is
still incomplete according to the responsibilities below.

## Remaining implementation sequence

Each item may need more than one reviewable slice. Audit dependencies before fixing slice size.

1. **Installation discovery and readiness with application-owned state.** Establish installation
   identity, root, validation results, and actionable UI state from real installed files. Specify
   ownership and cancellation before adding asynchronous operations. Do not represent mere file
   presence as authenticated launch eligibility. Suggested next branch:
   `bernie/launcher_installation_readiness`.
2. **Trusted installation/update/repair lifecycle.** Establish release-manifest trust, version and
   integrity contracts, staging, interrupted-operation recovery, atomic activation, rollback policy,
   disk-space handling, process/file ownership, and cancellation. Release endpoints and signing
   authority must be real deployment inputs. Keep launcher self-update with packaging where it
   requires executable replacement or platform signing; do not implement a pretend updater.
3. **Shared game settings and launcher preferences.** Audit native `GameSetUp.ini` and current game
   behavior, then implement one game-owned contract, validated defaults, atomic persistence,
   corruption/forward-version behavior, and supported pre-launch editing.
4. **Native AccountServer login and server selection.** Audit native/deob evidence and the current
   read-only server contract before implementing framing, packets, crypto, result semantics, and
   credential lifetimes. Establish a runtime catalog source without restoring `Server.dat`.
   Registration must follow evidence or an explicitly documented product equivalent.
5. **Controlled Play and game-process handoff.** Enforce installation and native-session eligibility,
   private local IPC, failure/timeouts, stale-session disposal, single/repeated launch behavior,
   launcher closure, client startup failure, process exit, and the explicit development bypass.
   Keep changes to the game composition root limited to the required bootstrap boundary.
6. **Complete presentation and launcher-wide review.** Finish real status/progress/login/settings
   flows, keyboard/accessibility/scaling, actionable errors, and diagnostics through thin UI code.
   Audit every responsibility together; final UI polish does not substitute for working backends.

For every slice: run locked restore, formatting, Release build/tests, content verification, both
publishes, payload-isolation and `Server.dat` rejection checks, whitespace/security searches, and a
full diff/adjacent-boundary review. Add platform-specific validation where the slice needs it; a
macOS local run does not establish Windows or Linux runtime behavior.

Stop at each healthy commit boundary. The user owns all commits and PR operations. After the user
confirms the commit/merge and updated main, remind them to upload a fresh codebase ZIP and continue
with the next audited slice. Do not declare launcher completion until the entire vertical passes a
final evidence, implementation, security, lifecycle, UI, test, and documentation audit.
