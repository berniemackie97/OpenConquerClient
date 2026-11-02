# Foundation audit

The initial ownership decision used these clean, verified repository snapshots on 2026-11-02:

| Source | Revision |
| --- | --- |
| OpenConquerClient main | `ac1a63ca6b6e1aeeefdebdfe6072282f33c83034` |
| OpenConquerServer main | `a16cae6a2942dc2883131645b766b89e73ae145e` |
| Client PR #196 head | `8401a8265f95dc2af2740d1abf4eac39ec3e8cd9` |

GitHub main matched the two local checkouts. Work takes place in an isolated client worktree.
This work does not edit synced project sources or the original client/server worktrees. PR #196 was
read without modifying or merging its branch. The source archives remain reference inputs;
this audit does not claim a new exhaustive analysis of their contents or binary formats.

## Ownership findings

| Evidence inspected | Finding and decision |
| --- | --- |
| Client project references, `Content/Images`, ANI loaders, WDF source, image tests, WORLD-007 | TGA/DDS decoding and RGBA pixels already exist. Move those implementations into shared Imaging; retain existing Content adapters and tests. |
| `Content/Media/EmbeddedMedia.cs`, account artwork APIs and embedded resources | Existing converted client images have explicit dimensions and integrity checks. Import supported images through those APIs; do not duplicate their bytes, metadata hashes, or decompression implementation. |
| `Rendering/OpenGL/OpenGLGraphicsDevice.cs`, `OpenGLRenderer.cs`, texture and sprite resources | Reusable OpenGL 3.3 Core rendering exists. Presentation targets framebuffer zero and assumes host-context ownership; embedded scene preview needs a deliberate host adapter and conformance work. Use an actual pixel preview for the texture slice. |
| `Platform/Windowing/Desktop`, `Platform/OpenGL` | The game host owns a standalone window, input devices, frame pacing, and context lifecycle. Keep that host out of the desktop authoring domain. |
| Launcher project, desktop tests, package versions | Avalonia is already pinned and used. Reuse that dependency baseline and interaction-testing approach. |
| Launcher managed manifest/signature/verifier, Product Tool, content-distribution contract | Existing authenticated release and installation ownership must remain authoritative. Defer signing/publishing integration; add no competing trust scheme. |
| PR #196's model, reader/writer, compiler, changed-file inventory, body and discussion | Indexed map sections, SHA-256 checks, deduplicated resource table, bounded writing, and window reads exist on the branch. The importer rejects exits; dependent asset migration and runtime adoption remain open. Whole-section validation still applies to small windows. Do not freeze or import that candidate as Studio's universal map model. |
| Server project references, game-data tool, map terrain/collision importers, source-directory checks, content docs | Server map definitions, collision, exits, and gameplay identity have separate authoritative consumers. Studio has no server dependency; do not generalize those models into an editor scene or copy their parsers. |
| Both repositories' build props, lockfiles, formatting/analyzer rules, client workflows | Preserve .NET 10/C# 14, warning-as-error builds, locked dependencies, formatting, and existing client gates. Add Studio workflow tests on all three desktop operating systems. |

## Review scope

The implementation review covers immutable identity and edit state, strict JSON input,
bounded decoding, source preservation, atomic-save commit order, exclusive session ownership,
staged non-overwriting builds, cancellation, bitmap disposal, operation serialization, and
shutdown. Automated coverage includes generated TGA/DDS files, existing supported artwork
imports, known pixel transforms, persistence, corruption, invalid metadata, failed writes,
duplicate IDs, linked paths, content reproducibility, CLI subprocesses, and desktop controls.

Native macOS smoke validation opened a project through the folder picker, previewed existing
client artwork, applied and saved an edit, built and validated content, created a new texture,
and saved it through the close dialog. A separate CLI process reopened the saved result and
validated both desktop and subsequent CLI builds. The final build also replaced an imported
artwork image through the native file picker, preserving its GUID, edit settings, and original
source association through save and a separate CLI reopen/build.

The final client base advanced to `ee9cd034432fee67c4f92583e7f57e6598d4b82b` during implementation.
Its login-only changes were inspected before rebasing; they do not alter Studio ownership.

The extraction must pass the full existing client solution; Studio tests alone are insufficient.
Headless UI tests are distinct from native desktop smoke checks. Windows link creation is
skipped when its required privilege is not provisioned. Native file pickers, accessibility,
display scaling, and future GPU viewport behavior require platform-specific manual checks.

The next integration must establish explicit game resource bindings and preserve UI semantics
before the client consumes Studio-built replacements. Imported images are not a completed
UI, map, animation, packaging, or release migration.

## Recorded validation

At the foundation boundary, macOS Apple Silicon passed locked restores, formatting,
warning-as-error builds, 3,323 existing client tests, 60 Studio tests, and three CLI subprocess
workflows. The unchanged decoder algorithms were compared against the original source, and
existing external package versions/hashes were checked for equality. Studio CI additionally
runs build, tests, independent content compilation/verification, and publish-boundary checks
on Windows, macOS, and Linux. CI and manual desktop checks are separate evidence.
