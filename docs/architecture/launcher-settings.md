# Launcher display settings

The launcher stores non-secret desktop preferences. They play no part in installation trust or
account identity. The start buttons pass them to the client as command-line options (see
[Starting the game](launcher-play-screen.md#starting-the-game)), and the client validates them.
Retail `ini/GameSetUp.ini` is not modified.

## File format and location

`display.json` lives under `OpenConquer/Launcher` in:

| Platform | Configuration root |
| --- | --- |
| Windows | Current user's LocalApplicationData |
| macOS | `~/Library/Application Support` |
| Linux | Absolute `XDG_CONFIG_HOME`, otherwise `~/.config` |

```json
{"schemaVersion":1,"width":1280,"height":720,"windowMode":"resizable","presentation":"fit"}
```

Width and height are in desktop pixels. They must be positive integers, at most 16384 per side and
33,177,600 pixels in total, which matches the client's current startup limits. Window modes are
`resizable`, `fixed`, and `fullscreen`. Presentation is `fit`, `integer`, or `stretch`. The defaults
are shown above. The client's compatibility render surface is separate from these desktop
preferences.

Reads are limited to 4096 bytes. Schema 1 rejects duplicate or unknown fields and invalid values. A
file with a newer schema is left as it is. If the file is missing, the defaults are used and no file
is created. A damaged schema-1 file has to be reset with **Restore defaults** and then **Save**.
Cancel leaves the original file untouched. Unsupported, oversized, or inaccessible files are never
overwritten: fix the access problem or move the file aside, then use Reload. A link or special file
at the final path is rejected. On Unix, the file is opened with no-follow and nonblocking flags so
that a FIFO cannot hang shutdown. This is preference storage. It does not protect the file from the
same user changing their own configuration directory.

## Writes and lifetime

The writer belongs to the launcher instance that holds the existing per-user single-instance lease.
One store runs its operations one at a time. Each editor keeps the revision of the content it
loaded. If the file has changed since then, the editor must Reload before it can save. Edit the file
with other programs only while the launcher is closed. The revision check is not an atomic
compare-and-swap, so it cannot detect another process writing during the final replacement.

Save writes a uniquely named file in the same directory, flushes it, and then atomically replaces
`display.json`. On Unix the file is created with mode 0600. Readers always see either the complete
old document or the complete new one. Cancelling before the replacement keeps the destination and
removes the temporary file. The replacement is the commit point, and success is returned after it.
This makes the change atomic for readers. It does not make it durable on every filesystem or
through a sudden power loss. Preferences contain no secrets and no release trust.

The dialog owns a draft and one I/O session. Closing the dialog cancels pending I/O and waits for it
to drain before the session's lifetime is disposed. The shutdown task is published before any close
callback can reenter it. Save, Cancel, Escape, and the window's close button close only the settings
dialog. Quitting the application drains the settings through the main window before the launcher
closes, including when the dialog is open.

Activating the launcher from a second instance brings the existing settings dialog forward.
Controls have accessible names. Tab moves between controls, Enter saves, and Escape cancels.
Validation and recovery messages use a polite live region. Unsaved edits never change the stored
preferences.

## Verification

Storage tests cover malformed, future-schema, and missing files, size limits, links and FIFOs,
atomic replacement, stale and concurrent saves, cancellation, and Unix permissions. Session tests
cover pending I/O, reentrant and repeated shutdown, and propagation of unexpected failures. Manual
checks on a real desktop must also cover Save, Cancel, Escape, the window close button, reopening
and persistence, and activation while settings are open. The launcher process must keep running
after each dialog close and exit cleanly when quit.
