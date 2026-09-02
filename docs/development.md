# Development

OpenConquer Client targets the .NET SDK pinned in [`global.json`](../global.json).

## Build

Restore dependencies:

```bash
dotnet restore OpenConquer.Client.slnx
```

Build the complete solution:

```bash
dotnet build OpenConquer.Client.slnx
```

Use a Release build when reproducing CI locally:

```bash
dotnet build OpenConquer.Client.slnx --configuration Release --no-restore
```

## Running

By default, the client treats the executable directory as its client-content root.

A different client tree can be supplied explicitly:

```bash
dotnet run \
  --project src/OpenConquer.Client/OpenConquer.Client.csproj \
  -- \
  --content-root /path/to/client
```

Relative `--content-root` values are resolved against the working directory from which the process
is launched.

The content root must contain the legacy files required by the implemented startup path. The current
startup requires:

```text
ini/GameSetup.Ini
```

with a valid:

```ini
[ScreenModeRecord]
ScreenMode=<0-3>
```

The screen-mode value selects the logical game resolution while remaining independent of the
physical resizable desktop-host size.

Unknown startup arguments, duplicate `--content-root` declarations, and missing option values are
rejected rather than ignored.

## Formatting

Check formatting without modifying files:

```bash
dotnet format OpenConquer.Client.slnx --verify-no-changes --no-restore
```

Apply formatting:

```bash
dotnet format OpenConquer.Client.slnx
```

## Tests

Test projects are added when a subsystem has behavior with meaningful invariants to verify. Empty
test-project scaffolding is not retained.

The current test projects are:

```text
tests/OpenConquer.Client.Tests
tests/OpenConquer.Content.Tests
```

`OpenConquer.Client.Tests` verifies executable startup policy, including content-root defaults,
explicit overrides, path normalization, and malformed argument handling.

`OpenConquer.Content.Tests` verifies legacy content-root lookup semantics and retail startup
configuration behavior, including case-insensitive lookup, path rejection, missing-content behavior,
and screen-mode mapping.

Run an individual test project directly while working on its subsystem:

```bash
dotnet test tests/OpenConquer.Client.Tests/OpenConquer.Client.Tests.csproj
dotnet test tests/OpenConquer.Content.Tests/OpenConquer.Content.Tests.csproj
```

CI discovers projects matching `tests/**/*.Tests.csproj` and executes each discovered project.

A test project must contain real tests. A zero-test test assembly is considered a failed test run
rather than a successful placeholder.

## Continuous Integration

GitHub Actions runs CI for pushes to `main`, pull requests, and manual workflow dispatches.

The Linux quality job runs on Ubuntu 24.04 and performs:

1. dependency restore
2. formatting verification
3. Release build
4. execution of every discovered test project

The Release build runs with the repository's analyzers and warnings-as-errors configuration.

Additional Windows 2025 and macOS 15 jobs restore and build the complete solution in Release
configuration so cross-platform compilation remains continuously verified. Their check names are
kept independent of runner labels so repository rules can require stable status-check identities.

CI obtains the .NET SDK selection from `global.json` rather than duplicating the SDK version in the
workflow.

GitHub Actions dependencies are pinned to immutable commit SHAs. Dependabot checks GitHub Actions
weekly so pinned actions can be reviewed and advanced deliberately when new releases are available.

## Dependency Review

Pull requests targeting `main` run a dependency-review workflow. The workflow fails when a
dependency change introduces a package with a known vulnerability according to GitHub's dependency
review.

NuGet dependencies are also checked weekly by Dependabot. The centrally managed Silk.NET packages
are grouped into a single update pull request when compatible updates are available.

GitHub dependency graph and automatic dependency submission are enabled for the repository. For
.NET, automatic dependency submission resolves build-time and transitive dependencies and submits
them to the dependency graph, giving dependency review and Dependabot more complete dependency data
than static manifest analysis alone.

## Code Scanning

CodeQL uses GitHub default setup so the scanning configuration follows GitHub's supported C#
defaults without adding a hand-maintained CodeQL workflow to the repository.

Code scanning should become a required repository rule only after its baseline has been verified
healthy and stable enough to act as a merge gate.

## Repository Protection

`main` is protected by the repository's active `Main protection` ruleset.

Changes to `main` require a pull request and the following status checks:

- `Quality`
- `Build (Windows)`
- `Build (macOS)`
- `Dependency Review`

Required checks use strict branch-update enforcement so the candidate branch must be current with
`main` before merge.

The ruleset also:

- requires linear history
- restricts deletion of `main`
- blocks force pushes
- has no bypass actors

Repository policy is configured in GitHub rather than duplicated as source-controlled workflow
logic.

Because linear history is required, production work should normally be integrated using squash or
rebase semantics rather than merge commits.

## Packages

NuGet versions are managed centrally in:

```text
Directory.Packages.props
```

Project files should reference packages without specifying their version:

```xml
<PackageReference Include="Silk.NET.OpenGL" />
```

## Project Settings

Repository-wide compiler and analyzer settings live in:

```text
Directory.Build.props
.editorconfig
```

Repository checkout line-ending normalization is defined by:

```text
.gitattributes
```

Text files use LF line endings across development platforms, while Windows command scripts retain
CRLF line endings.

## Before Committing

Restore once:

```bash
dotnet restore OpenConquer.Client.slnx
```

Then run the same core quality checks enforced by CI:

```bash
dotnet format OpenConquer.Client.slnx --verify-no-changes --no-restore
dotnet build OpenConquer.Client.slnx --configuration Release --no-restore
```

Run every affected test project:

```bash
dotnet test tests/OpenConquer.Client.Tests/OpenConquer.Client.Tests.csproj \
  --configuration Release \
  --no-build \
  --no-restore

dotnet test tests/OpenConquer.Content.Tests/OpenConquer.Content.Tests.csproj \
  --configuration Release \
  --no-build \
  --no-restore
```

Finally, verify that the diff contains no whitespace errors:

```bash
git diff --check
```
