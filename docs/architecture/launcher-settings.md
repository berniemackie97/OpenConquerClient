# Launcher display settings

The launcher stores non-secret desktop preferences. They do not establish installation trust or
account identity. This slice persists preferences; controlled client startup must deliver and
validate them in its own implementation. Retail `ini/GameSetUp.ini` is unchanged.

## File contract

`display.json` lives under `OpenConquer/Launcher` in:

| Platform | Configuration root |
| --- | --- |
| Windows | Current user's LocalApplicationData |
| macOS | `~/Library/Application Support` |
| Linux | Absolute `XDG_CONFIG_HOME`, otherwise `~/.config` |

```json
{"schemaVersion":1,"width":1280,"height":720,"windowMode":"resizable","presentation":"fit"}
```

Width/height are desktop pixels: positive integers, at most 16384 per side and 33,177,600 pixels
in total, matching the current client startup limits. Modes are `resizable`, `fixed`, `fullscreen`;
presentation is `fit`, `integer`, `stretch`. Defaults are shown above. The client's compatibility
render surface is separate from these desktop preferences.

Reads are bounded to 4096 bytes. Schema 1 rejects duplicate/unknown fields and invalid values.
Newer schemas are preserved. Missing files use defaults without creating a file. Damaged schema-1
files require **Restore defaults**, then **Save**; Cancel leaves the original untouched. Unsupported,
oversized or inaccessible files are not overwritten. Fix access or move the file aside, then Reload.
Final-path links and special files are rejected; Unix opens use no-follow and nonblocking flags so
FIFO input cannot hang shutdown. This is preference storage, not a security boundary against the
same user modifying their configuration directory.

## Writes and lifetime

The launcher's existing user-scoped single-instance lease owns the writer. One store serializes its
operations. Each editor retains a content revision; a changed file requires Reload before saving.
External editors should run with the launcher closed: revision checking is not an atomic
compare-and-swap against unrelated processes writing during the final replacement.

Save writes a unique file in the same directory, flushes it, then atomically replaces `display.json`.
Unix files are created with mode 0600. Readers see complete old or new documents. Cancellation
before replacement preserves the destination and removes the temporary file; replacement is the
commit point, after which success is returned. This guarantees atomic visibility, not durability
across every filesystem or sudden power loss. Preferences carry no secrets or release authority.

The dialog owns a draft and one I/O session. Closing cancels and drains pending I/O before disposing
its lifetime. The shutdown task is published before any close callback can reenter it. Save, Cancel,
Escape and the native close button close only settings. Application quit drains settings through the main window before closing the launcher, including
when the dialog is open.
Second-instance activation brings the existing settings dialog forward. Controls expose accessible
names; Tab navigates, Enter saves and Escape cancels. Validation/recovery status uses a polite live
region. Unsaved edits never update stored preferences.

## Verification

Storage tests cover malformed/future/missing files, size limits, links/FIFOs, atomic replacement,
stale/concurrent saves, cancellation and Unix permissions. Session tests cover pending I/O,
reentrant and repeated shutdown, and unexpected failure propagation. Native desktop checks must
also verify Save, Cancel, Escape, native close, reopen/persistence and activation with settings open;
the launcher process must remain alive after each dialog close and exit cleanly when quit.
