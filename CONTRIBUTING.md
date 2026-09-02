# Contributing

Contributions are welcome.

OpenConquer is still early in development, so larger changes should fit the existing architecture
rather than introducing new project boundaries or abstractions without a clear need.

## Development Workflow

Changes should be developed on a focused branch and submitted through a pull request. Direct pushes
to `main` are not part of the normal development workflow.

Before opening or updating a pull request, restore dependencies once:

```bash
dotnet restore OpenConquer.Client.slnx
```

Check formatting without restoring again:

```bash
dotnet format OpenConquer.Client.slnx --verify-no-changes --no-restore
```

Build the complete solution in the same configuration used by CI:

```bash
dotnet build OpenConquer.Client.slnx --configuration Release --no-restore
```

Run every relevant test project. Test projects should contain real tests when they are introduced;
empty test-project scaffolding is not kept in the repository.

Finally, verify that the diff contains no whitespace errors:

```bash
git diff --check
```

Pull requests must pass the repository's required CI, cross-platform build, and dependency-review
checks before merge.

## Code

A few general rules:

- warnings are treated as errors
- keep platform and graphics APIs out of gameplay code
- avoid allocations and blocking work in hot frame paths
- prefer clear game terminology over native/decompiler terminology
- keep ownership and resource lifetime explicit
- do not add catch-all projects such as `Core`, `Common`, or `Utilities`
- do not introduce an interface or abstraction unless it represents a real boundary
- do not hide required production behavior behind silent no-op implementations
- keep changes focused

Reverse-engineered behavior from the original client may be used to establish compatibility, but the
new implementation should not reproduce the original client architecture simply because retail
implemented something that way.

## Project Structure

The current production projects are:

```text
OpenConquer.Client
OpenConquer.Content
OpenConquer.Gameplay
OpenConquer.Networking
OpenConquer.Platform
OpenConquer.Rendering
```

Adding another assembly should represent a clear ownership boundary and should be discussed before
implementation.

See [`docs/architecture/architecture.md`](docs/architecture/architecture.md) for the current
architecture.
