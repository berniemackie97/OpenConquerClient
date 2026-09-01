# Development

OpenConquer Client targets the .NET SDK pinned in [`global.json`](../global.json).

## Build

```bash
dotnet restore
dotnet build OpenConquer.Client.slnx
```

## Formatting

Check formatting without modifying files:

```bash
dotnet format OpenConquer.Client.slnx --verify-no-changes
```

Apply formatting:

```bash
dotnet format OpenConquer.Client.slnx
```

## Tests

```bash
dotnet test tests/OpenConquer.Gameplay.Tests/OpenConquer.Gameplay.Tests.csproj
```

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

Repository wide compiler and analyzer settings live in:

```text
Directory.Build.props
.editorconfig
```

## Before Committing

Run:

```bash
dotnet format OpenConquer.Client.slnx --verify-no-changes
dotnet build OpenConquer.Client.slnx
```

Run the relevant tests for any systems changed by the commit.
