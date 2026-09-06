# Development

OpenConquer Client targets the .NET SDK pinned by [`global.json`](../global.json).

For architecture and ownership rules, see
[`architecture/architecture.md`](architecture/architecture.md). For the installed launcher/product
layout, see
[`architecture/launcher-managed-installation.md`](architecture/launcher-managed-installation.md).

## Build

Restore locked dependencies:

```bash
dotnet restore OpenConquer.Client.slnx --locked-mode
```

Build the solution:

```bash
dotnet build OpenConquer.Client.slnx \
  --configuration Release \
  --no-restore
```

Verify formatting:

```bash
dotnet format OpenConquer.Client.slnx \
  --verify-no-changes \
  --no-restore
```

Run the complete test suite:

```bash
dotnet test OpenConquer.Client.slnx \
  --configuration Release \
  --no-build \
  --no-restore
```

Repository builds use warnings as errors and the analyzers configured in `Directory.Build.props` and
`.editorconfig`.

## Game Client

Run the game client directly during development:

```bash
dotnet run \
  --project src/OpenConquer.Client/OpenConquer.Client.csproj
```

The default development build stages the current verified retail runtime content beneath:

```text
content/retail-5517/payload
```

The implemented runtime content closure is:

```text
data/main/Logo1.bmp
data/main/Logo2.bmp
ini/GameSetUp.ini
ini/info.ini
ini/package.ini
```

Retail `Server.dat` is not runtime content. It is retained only as offline compatibility evidence
and is consumed by `OpenConquer.Content.Tool.Legacy.ServerDat`.

An explicit development content root may be supplied with:

```bash
dotnet run \
  --project src/OpenConquer.Client/OpenConquer.Client.csproj \
  -- \
  --content-root /path/to/client
```

Current desktop-development options are:

```text
--window-size WIDTHxHEIGHT
--window-mode resizable|fixed|fullscreen
--presentation fit|integer|stretch
```

`ini/GameSetUp.ini` continues to provide the legacy-compatible logical render surface. The physical
desktop window size is a separate host concern.

Inspect a historical `Server.dat` explicitly with:

```bash
dotnet run \
  --project tools/OpenConquer.Content.Tool \
  -- \
  inspect-server-dat \
  --file /path/to/Server.dat
```

This command is tooling only and does not provide runtime server discovery.

## Launcher

`OpenConquer.Launcher` and `OpenConquer.Client` are published independently and then composed into
one managed product.

Create a local staged product with:

```bash
rm -rf \
  /tmp/openconquer-client-publish \
  /tmp/openconquer-launcher-publish \
  /tmp/openconquer-managed

dotnet publish \
  src/OpenConquer.Client/OpenConquer.Client.csproj \
  --configuration Release \
  --no-restore \
  --output /tmp/openconquer-client-publish

dotnet publish \
  src/OpenConquer.Launcher/OpenConquer.Launcher.csproj \
  --configuration Release \
  --no-restore \
  --output /tmp/openconquer-launcher-publish

dotnet run \
  --project tools/OpenConquer.Product.Tool \
  --configuration Release \
  --no-build \
  --no-restore \
  -- \
  stage-managed-product \
  --launcher-publish /tmp/openconquer-launcher-publish \
  --client-publish /tmp/openconquer-client-publish \
  --output /tmp/openconquer-managed
```

The raw launcher publish must not contain installed-product layout:

```bash
test ! -e /tmp/openconquer-launcher-publish/openconquer.installation.json
test ! -e /tmp/openconquer-launcher-publish/client
```

The composed product must contain it:

```bash
test -f /tmp/openconquer-managed/openconquer.installation.json
test -d /tmp/openconquer-managed/client
```

Run the staged launcher on macOS/Linux with:

```bash
/tmp/openconquer-managed/OpenConquer.Launcher
```

A raw launcher build or publish is not an installed OpenConquer product and therefore does not have
a managed client component.

The launcher resolves the installed layout automatically. After an expected failure, **Check again**
rechecks the same root without restarting. It does not repair files or verify release integrity.
**Close** and the native close control drain active work and exit. See the
[installation contract](architecture/launcher-managed-installation.md) for lifecycle and smoke checks.

Update/repair, native AccountServer login, settings, and controlled game handoff are not implemented.

## Content Verification

Verify the tracked retail content set:

```bash
dotnet run \
  --project tools/OpenConquer.Content.Tool \
  --configuration Release \
  --no-build \
  --no-restore \
  -- \
  verify-content-set \
  --content-set content/retail-5517
```

Verify a published client:

```bash
dotnet run \
  --project tools/OpenConquer.Content.Tool \
  --configuration Release \
  --no-build \
  --no-restore \
  -- \
  verify-content-set \
  --content-set /tmp/openconquer-client-publish/content/retail-5517
```

Production client publishes must not contain `Server.dat`:

```bash
find \
  /tmp/openconquer-client-publish \
  -type f \
  -iname 'Server.dat' \
  -print
```

The command must print nothing.

## Tests

Test projects exist only where there are meaningful invariants to verify:

```text
tests/OpenConquer.Client.Tests
tests/OpenConquer.Content.Tests
tests/OpenConquer.Content.Tool.Tests
tests/OpenConquer.Launcher.Tests
tests/OpenConquer.Platform.Tests
tests/OpenConquer.Product.Tool.Tests
tests/OpenConquer.Rendering.Tests
```

The important ownership split is:

- client tests verify startup and game-runtime composition;
- content tests verify runtime content and legacy-format behavior;
- launcher tests verify launcher lifecycle and product-boundary behavior;
- product-tool tests verify deterministic managed-product staging;
- platform tests verify desktop host mechanics;
- rendering tests verify graphics behavior independently of a live desktop where possible.

Platform-specific behavior still requires the repository's Windows and macOS CI jobs where local
development cannot cover it.

## Continuous Integration

GitHub Actions verifies:

1. locked restore;
2. formatting;
3. Release build;
4. complete solution tests;
5. tracked retail content integrity;
6. client publication and published-content integrity;
7. absence of runtime `Server.dat`;
8. independent launcher publication;
9. absence of installed-product layout and game content from the raw launcher publish;
10. managed-product composition;
11. Windows and macOS solution build/test coverage.

Publishing is treated as a separate product guarantee from compilation.

GitHub Actions dependencies are pinned to immutable commit SHAs. NuGet versions are centrally
managed through `Directory.Packages.props`.

## Before Committing

Run the complete local quality gate:

```bash
dotnet restore OpenConquer.Client.slnx --locked-mode

dotnet format OpenConquer.Client.slnx \
  --verify-no-changes \
  --no-restore

dotnet build OpenConquer.Client.slnx \
  --configuration Release \
  --no-restore

dotnet test OpenConquer.Client.slnx \
  --configuration Release \
  --no-build \
  --no-restore

dotnet run \
  --project tools/OpenConquer.Content.Tool \
  --configuration Release \
  --no-build \
  --no-restore \
  -- \
  verify-content-set \
  --content-set content/retail-5517

rm -rf \
  /tmp/openconquer-client-publish \
  /tmp/openconquer-launcher-publish \
  /tmp/openconquer-managed

dotnet publish \
  src/OpenConquer.Client/OpenConquer.Client.csproj \
  --configuration Release \
  --no-restore \
  --output /tmp/openconquer-client-publish

dotnet run \
  --project tools/OpenConquer.Content.Tool \
  --configuration Release \
  --no-build \
  --no-restore \
  -- \
  verify-content-set \
  --content-set /tmp/openconquer-client-publish/content/retail-5517

find \
  /tmp/openconquer-client-publish \
  -type f \
  -iname 'Server.dat' \
  -print

dotnet publish \
  src/OpenConquer.Launcher/OpenConquer.Launcher.csproj \
  --configuration Release \
  --no-restore \
  --output /tmp/openconquer-launcher-publish

test ! -e /tmp/openconquer-launcher-publish/openconquer.installation.json
test ! -e /tmp/openconquer-launcher-publish/client

find \
  /tmp/openconquer-launcher-publish \
  -path '*/content/retail-5517*' \
  -print

dotnet run \
  --project tools/OpenConquer.Product.Tool \
  --configuration Release \
  --no-build \
  --no-restore \
  -- \
  stage-managed-product \
  --launcher-publish /tmp/openconquer-launcher-publish \
  --client-publish /tmp/openconquer-client-publish \
  --output /tmp/openconquer-managed

test -f /tmp/openconquer-managed/openconquer.installation.json
test -d /tmp/openconquer-managed/client
test -f /tmp/openconquer-managed/client/content/retail-5517/manifest.json

git diff --check
```

Both `find` commands must print nothing.

Commit only after the implementation, tests, documentation, publish boundaries, and applicable
platform verification for the current work slice are clean.
