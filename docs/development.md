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

CI discovers projects matching `tests/**/*.Tests.csproj` and executes each discovered project.

Run an individual test project directly while working on its subsystem:

```bash
dotnet test tests/<Project>.Tests/<Project>.Tests.csproj
```

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

GitHub dependency graph and automatic dependency submission should remain enabled for the
repository. For .NET, automatic dependency submission resolves build-time and transitive
dependencies and submits them to the dependency graph, giving dependency review and Dependabot more
complete dependency data than static manifest analysis alone.

## Code Scanning

CodeQL uses GitHub default setup so the scanning configuration follows GitHub's supported C#
defaults without adding a hand-maintained CodeQL workflow to the repository. Code scanning results
should be required by the `main` ruleset once the initial baseline analysis succeeds.

## Repository Protection

`main` is intended to be protected by a GitHub ruleset that requires pull requests and the following
status checks before merge:

- `Quality`
- `Build (Windows)`
- `Build (macOS)`
- `Dependency Review`

The ruleset should also require the branch to be up to date before merge, require linear history,
restrict deletion of `main`, and block force pushes. Once the initial CodeQL baseline succeeds, the
ruleset should require CodeQL results with non-security errors and warnings blocked and security
alerts of medium severity or higher blocked.

Repository policy is configured in GitHub rather than duplicated as source-controlled configuration.

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

Run every test project affected by the change.

Finally, verify that the diff contains no whitespace errors:

```bash
git diff --check
```
