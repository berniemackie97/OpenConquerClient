# Launcher instances

One launcher owns the product workflow per OS user, across desktop sessions and installation paths.
Opening it again requests activation of that user's existing window. This policy is separate from
any future machine-wide installation/update transaction lock.

## Startup and shutdown

1. Before opening diagnostics or Avalonia, `Program` attempts a user-scoped named mutex.
2. The owner creates the activation listener, then starts the desktop. It retains the mutex on the
   main thread until both desktop and listener shutdown have completed.
3. A second invocation requests activation and exits with code `0` after acknowledgement. If the
   owner exits during startup, it may acquire the released mutex and become the new owner.
4. After a five-second retry budget, an unresponsive owner produces a small explanatory window;
   closing that notice returns code `2`. It does not start installation work or another log writer.

The OS releases ownership after process termination. Once it owns the mutex, a replacement process
can recreate the Unix socket left by an interrupted owner. Ordinary shutdown cancels pending accepts,
reads, and queued UI activation before disposing the listener and releasing ownership. Unexpected
listener/application failures reach the existing redacted fatal-host boundary and return code `1`.

Activation restores a minimized window and requests foreground activation on the UI dispatcher.
The desktop window manager retains control of foreground-focus policy. Closing windows reject
activation; an aborted dispatcher request cannot reopen them later.

## Local channel

- Windows uses a named pipe; macOS/Linux use a short absolute socket path under `/tmp`, independent
  of shell-specific temporary directories. A hash of the user identity distinguishes endpoints.
- The mutex uses `CurrentUserOnly`, with session restriction disabled. Both pipe peers use
  `PipeOptions.CurrentUserOnly`; Unix socket permissions are additionally restricted to `0600`.
  Identity checks come from the OS, not the endpoint name or a client-supplied identifier.
- Each connection reads one eight-byte activation frame: `4F 43 4C 41 01 01 00 00` (magic, version,
  command, reserved bytes). The one-byte response is `01` for accepted or `02` for unavailable.
- Unknown frames and truncated connections are closed. No variable-length data is parsed; only one
  command is handled per connection. Reads and UI acknowledgement have a one-second deadline.
- The channel carries no arguments, paths, credentials, settings, or game-session data. It must not
  be reused for native account authentication or secure client handoff.

Runtime behavior follows [.NET pipe peer isolation](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.pipeoptions?view=net-10.0)
and [named mutex scoping](https://learn.microsoft.com/en-us/dotnet/api/system.threading.namedwaithandleoptions?view=net-10.0).

## Verification

`LauncherInstanceProcessTests` use the test-only `Launcher.InstanceProbe` executable to exercise
concurrent processes, activation, unresponsive ownership, normal release, and forced termination.
Transport tests cover invalid/truncated frames, idle peers, cancellation, application-failure
propagation, and Unix socket permissions. The probe is not part of either product publish.

Desktop smoke check: open a staged launcher, minimize it, and execute it again. The existing window
must return; the second process must exit successfully. Closing the owner must exit successfully,
and the next invocation must be able to become the owner.
