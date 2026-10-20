# Contributing

Contributions are welcome.

OpenConquer is early in development. Larger changes should fit the existing architecture. Add new
projects or abstractions only when there is a clear need for them.

## Development Workflow

Work on a focused branch and submit your changes through a pull request. Changes do not normally
reach `main` through direct pushes.

Before opening or updating a pull request, restore dependencies once:

```bash
dotnet restore OpenConquer.Client.slnx --locked-mode
```

CI restores in locked mode, so a restore that would change a lock file fails there. Commit a
dependency change together with the lock files it produces.

Check formatting without restoring again:

```bash
dotnet format OpenConquer.Client.slnx --verify-no-changes --no-restore
```

Build the whole solution in the Release configuration, as CI does:

```bash
dotnet build OpenConquer.Client.slnx --configuration Release --no-restore
```

Run the full test suite the same way CI does:

```bash
dotnet test OpenConquer.Client.slnx --configuration Release --no-build --no-restore
```

A new test project should contain real tests from the start. Empty test-project scaffolding is not
kept in the repository.

Finally, check the diff for whitespace errors:

```bash
git diff --check
```

A pull request can be merged only after it passes the repository's required CI, cross-platform
build, and dependency-review checks.

## Code

General rules:

- warnings are treated as errors
- keep platform and graphics APIs out of gameplay code
- avoid allocations and blocking work in hot frame paths
- prefer clear game terminology over names taken from the original binary or the decompiler
- make ownership and resource lifetime visible in the code
- do not add catch-all projects such as `Core`, `Common`, or `Utilities`
- add an interface or abstraction only when it represents a real boundary
- do not hide required production behavior behind silent no-op implementations
- keep changes focused

Reverse-engineered behavior from the original client can be used to match its behavior. The new
code should not copy the original client's architecture just because retail was built that way.

## Project Structure

The current production projects are:

```text
OpenConquer.Client
OpenConquer.Content
OpenConquer.Gameplay
OpenConquer.Launcher
OpenConquer.Networking
OpenConquer.Platform
OpenConquer.Rendering
```

A new assembly needs a clear ownership reason. Discuss it before starting the implementation.

See [`docs/architecture/architecture.md`](docs/architecture/architecture.md) for the current
architecture.
