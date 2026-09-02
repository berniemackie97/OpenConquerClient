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

GitHub Actions runs CI for:

- pushes to `main`
- pull requests
- manual workflow dispatches

The Linux quality job performs:

1. dependency restore
2. formatting verification
3. Release build
4. execution of every discovered test project

The Release build runs with the repository's analyzers and warnings-as-errors configuration.

Additional Windows and macOS jobs restore and build the complete solution in Release configuration
so cross-platform compilation remains continuously verified.

CI obtains the .NET SDK selection from `global.json` rather than duplicating the SDK version in the
workflow.

GitHub Actions dependencies are pinned to immutable commit SHAs.

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
