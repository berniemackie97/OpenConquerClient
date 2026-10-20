# Launcher instances

Each OS user runs at most one launcher, across all desktop sessions and installation paths, and
that launcher owns the product workflow. Starting the launcher again brings the user's existing
window forward. This rule is separate from any future machine-wide lock for installation or update
transactions.

## Startup and shutdown

1. Before opening diagnostics or Avalonia, `Program` resolves the activation namespace and tries to
   acquire a per-user named mutex.
2. The owner creates the activation listener and then starts the desktop. It holds the mutex on the
   main thread until both the desktop and the listener have shut down.
3. A second invocation asks the owner to activate and exits with code `0` once the owner
   acknowledges. If the owner exits while the second process is starting, the second process may
   acquire the released mutex and become the new owner.
4. If the owner still does not respond after five seconds of retries, the second process shows a
   small window that explains the problem. Closing it exits with code `2`. This process starts no
   installation work and no second log writer.

The OS releases the mutex when the owning process ends. Once a replacement process holds the mutex,
it can recreate the Unix socket left behind by an interrupted owner. Normal shutdown cancels pending
accepts, reads, and queued UI activation, then disposes the listener and releases the mutex.
Unexpected listener or application failures go to the existing redacted fatal-error handling and
exit with code `1`.

Activation restores a minimized window and requests foreground activation on the UI dispatcher. The
desktop window manager still decides whether the window actually takes focus. A window that is
closing rejects activation, and an aborted dispatcher request cannot reopen it later.

## Local channel

- Windows uses a named pipe whose name is derived from the user's identity. Unix uses the private
  namespace described below.
- The mutex uses `CurrentUserOnly` with session restriction turned off. Both pipe ends use
  `PipeOptions.CurrentUserOnly`. On Unix the socket's permissions are also restricted to `0600`.
  The OS performs the identity checks. The endpoint name and anything the client sends play no part
  in them.
- Each connection reads one eight-byte activation frame: `4F 43 4C 41 01 01 00 00` (magic, version,
  command, reserved bytes). The one-byte response is `01` for accepted or `02` for unavailable.
- Unknown frames and truncated connections are closed. Nothing of variable length is parsed, and
  each connection handles a single command. Reads and the UI acknowledgement must finish within one
  second.
- The channel carries no arguments, paths, credentials, settings, or game-session data. It must not
  be reused for game account authentication or for a secure handoff to the client.

The isolation relies on the documented behavior of [.NET pipe peer isolation](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.pipeoptions?view=net-10.0)
and [named mutex scoping](https://learn.microsoft.com/en-us/dotnet/api/system.threading.namedwaithandleoptions?view=net-10.0).

## Unix namespace

| Platform | Location |
| --- | --- |
| Linux | `$XDG_RUNTIME_DIR/openconquer/activate`; if unset, `/run/user/<effective UID>/openconquer/activate` |
| Linux without a runtime directory | `<validated home>/.openconquer-runtime/activate` |
| macOS | `confstr(_CS_DARWIN_USER_TEMP_DIR)` plus `openconquer/activate`; independent of `TMPDIR` |

A configured runtime root must already exist, belong to the effective UID, and have mode `0700`. An
invalid configured root is treated as an error. The home-directory fallback is used only when the
default `/run/user` location does not exist. The home directory must belong to the user, and other
users must not be able to write to it. Paths that are too long are rejected: the UTF-8 path may be
at most 107 bytes on Linux or 103 bytes on macOS.

`UnixActivationNamespace` walks the path from `/` with `openat`, using no-follow directory handles,
and checks ownership and mode on those handles. Every ancestor must belong to root or the effective
UID and must not be writable by group or others, except for root-owned sticky directories. On macOS,
extended ACLs that grant access are rejected, while deny-only ACLs are allowed. When resolving the
path the OS provides, Apple's fixed `/var` alias is mapped to `/private/var`. No other symlink is
ever followed.

The application directory is created atomically with `mkdirat(..., 0700)` and then validated again.
Existing permissions are never repaired. Before binding, the server validates its private parent
directory again. It accepts only a missing endpoint or a socket owned by the same user, and it never
replaces a symlink or a regular file. A stale socket is replaced only while the mutex is held.
Shutdown removes the socket and leaves the private directory in place for reuse. Protection between
users assumes that the OS, root, and the current user are trusted. A process running under the same
UID can already change that user's launcher and files.

The interop calls the public Linux `statx` and Darwin `stat64` ABIs on x64 and arm64 instead of
private .NET APIs. See the [XDG base directory specification](https://specifications.freedesktop.org/basedir/0.8/)
and [handle-relative filesystem metadata](https://man7.org/linux/man-pages/man2/statx.2.html).
There is no fallback to the old shared `/tmp/oc-launcher-*` endpoint, so close any old launcher
before starting the upgraded version. Activation works across sessions only when they use the same
per-user runtime location.

## Verification

`LauncherInstanceProcessTests` use the test-only `Launcher.InstanceProbe` executable to exercise
concurrent processes, activation, an unresponsive owner, normal release, and forced termination.
The probe is not part of either product publish. Transport tests cover invalid and truncated frames,
idle peers, cancellation, propagation of application failures, and Unix socket permissions.
Namespace tests cover unsafe modes, links in place of directories or endpoints, writable ancestors,
leaving other users' objects untouched, UTF-8 path limits, macOS ACLs, and recovery after process
termination on both Unix platforms.

On Linux, CI copies the complete built C# probe into a temporary root-owned directory that the test
UIDs can read and execute but not write, and runs it with `--verify-cross-uid`. This way the test
does not need access to the runner's private checkout. An exit trap removes the copy whether the
test passes or fails. The root coordinator uses `setpriv` to run the launcher as UID 65534 and the
attacker as UID 1, each with the matching GID, no supplementary groups or capabilities, and
privilege escalation disabled. Each process checks the identity and privileges the kernel reports
for it, and a missing prerequisite fails the check. Positive controls confirm that filesystem and
socket operations work in the attacker's own directory. The test requires permission-denied results
before startup, while the owner is listening, and after a forced crash. It also checks that
ownership is separate for each user, that normal activation works, that a stale same-user endpoint
is recovered, and that a runtime root belonging to another user is rejected. Throughout startup and
recovery, the attacker holds the old `/tmp` endpoint path. The test creates no user accounts.

**Run this test only in an isolated Linux VM or container.** To repeat it, build Release and run
the "Verify launcher isolation across Unix users" step from [CI](../../.github/workflows/ci.yml).
The installed .NET host and runtime must also be readable and executable by both test UIDs. Do not
loosen checkout, home, or activation-directory permissions to make the test pass.

Manual desktop check: start a staged launcher, minimize it, and start it again. The existing window
must come back and the second process must exit successfully. Closing the owner must also exit
successfully, and the next launch must be able to become the owner.
