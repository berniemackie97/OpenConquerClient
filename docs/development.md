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
one authenticated managed product.

A staged product requires:

- the independently published client;
- a release manifest describing the exact client byte tree;
- an externally produced ECDSA P-256/SHA-256 signature over the exact manifest bytes;
- a validated release-signature envelope;
- the independently published launcher with the corresponding public publisher trust embedded; and
- final product composition through `OpenConquer.Product.Tool`.

The following local workflow creates an **ephemeral development publisher identity**. It is suitable
for local verification only. Production release-signing private keys must be separately controlled
and must never be committed, embedded into the launcher, or packaged into the managed product.

Create a local staged product on macOS/Linux with:

```bash
rm -rf \
  /tmp/openconquer-client-publish \
  /tmp/openconquer-launcher-publish \
  /tmp/openconquer-release \
  /tmp/openconquer-managed

mkdir -p /tmp/openconquer-release

case "$(uname -s):$(uname -m)" in
  Darwin:arm64)
    TARGET_RUNTIME=osx-arm64
    ;;
  Darwin:x86_64)
    TARGET_RUNTIME=osx-x64
    ;;
  Linux:x86_64)
    TARGET_RUNTIME=linux-x64
    ;;
  Linux:aarch64|Linux:arm64)
    TARGET_RUNTIME=linux-arm64
    ;;
  *)
    echo "Unsupported local release runtime: $(uname -s):$(uname -m)" >&2
    exit 1
    ;;
esac

dotnet publish \
  src/OpenConquer.Client/OpenConquer.Client.csproj \
  --configuration Release \
  --no-restore \
  --output /tmp/openconquer-client-publish

dotnet run \
  --project tools/OpenConquer.Product.Tool \
  --configuration Release \
  --no-build \
  --no-restore \
  -- \
  create-release-manifest \
  --client-publish /tmp/openconquer-client-publish \
  --target-runtime "$TARGET_RUNTIME" \
  --release-version dev-local \
  --release-sequence 1 \
  --minimum-launcher-version 1 \
  --output /tmp/openconquer-release/openconquer.release.json

umask 077

openssl ecparam \
  -name prime256v1 \
  -genkey \
  -noout \
  -out /tmp/openconquer-release/publisher-private.pem

openssl pkey \
  -in /tmp/openconquer-release/publisher-private.pem \
  -pubout \
  -outform DER \
  -out /tmp/openconquer-release/publisher-public.der

openssl dgst \
  -sha256 \
  -sign /tmp/openconquer-release/publisher-private.pem \
  -out /tmp/openconquer-release/openconquer.release.der \
  /tmp/openconquer-release/openconquer.release.json

dotnet run \
  --project tools/OpenConquer.Product.Tool \
  --configuration Release \
  --no-build \
  --no-restore \
  -- \
  create-release-signature \
  --release-manifest /tmp/openconquer-release/openconquer.release.json \
  --public-key /tmp/openconquer-release/publisher-public.der \
  --signature /tmp/openconquer-release/openconquer.release.der \
  --output /tmp/openconquer-release/openconquer.release.sig

PUBLIC_KEY="$(
  openssl base64 \
    -A \
    -in /tmp/openconquer-release/publisher-public.der
)"

printf \
  '{"schemaVersion":1,"keys":[{"algorithm":"ecdsa-p256-sha256-der","publicKey":"%s"}]}\n' \
  "$PUBLIC_KEY" \
  > /tmp/openconquer-release/release-trust.json

dotnet publish \
  src/OpenConquer.Launcher/OpenConquer.Launcher.csproj \
  --configuration Release \
  --no-restore \
  --output /tmp/openconquer-launcher-publish \
  -p:OpenConquerReleaseTrustPath=/tmp/openconquer-release/release-trust.json

dotnet run \
  --project tools/OpenConquer.Product.Tool \
  --configuration Release \
  --no-build \
  --no-restore \
  -- \
  stage-managed-product \
  --launcher-publish /tmp/openconquer-launcher-publish \
  --client-publish /tmp/openconquer-client-publish \
  --release-manifest /tmp/openconquer-release/openconquer.release.json \
  --release-signature /tmp/openconquer-release/openconquer.release.sig \
  --output /tmp/openconquer-managed

rm -f /tmp/openconquer-release/publisher-private.pem
```

The supported release runtime identities are:

```text
win-x64
win-arm64
osx-x64
osx-arm64
linux-x64
linux-arm64
```

The raw launcher publish must not contain mutable installed-product layout or release metadata:

```bash
test ! -e /tmp/openconquer-launcher-publish/openconquer.installation.json
test ! -e /tmp/openconquer-launcher-publish/openconquer.release.json
test ! -e /tmp/openconquer-launcher-publish/openconquer.release.sig
test ! -e /tmp/openconquer-launcher-publish/release-trust.json
test ! -e /tmp/openconquer-launcher-publish/client
```

Publisher trust is embedded into the launcher assembly through `OpenConquerReleaseTrustPath`; it is
not shipped as a mutable loose `release-trust.json` file.

The composed product must contain the authenticated release metadata and client:

```bash
test -f /tmp/openconquer-managed/openconquer.installation.json
test -f /tmp/openconquer-managed/openconquer.release.json
test -f /tmp/openconquer-managed/openconquer.release.sig
test -d /tmp/openconquer-managed/client
```

The development publisher material must not be present in the composed product:

```bash
test ! -e /tmp/openconquer-managed/publisher-private.pem
test ! -e /tmp/openconquer-managed/publisher-public.der
test ! -e /tmp/openconquer-managed/release-trust.json
```

Run the staged launcher on macOS/Linux with:

```bash
/tmp/openconquer-managed/OpenConquer.Launcher
```

A raw launcher build or publish is not an installed OpenConquer product and therefore does not have
a managed client component. A launcher published without configured embedded release trust also
fails closed when asked to resolve a signed managed release.

The launcher resolves the installed layout automatically and verifies the release manifest,
publisher signature, target runtime, complete client file set, file lengths, and SHA-256 hashes
before reporting the installation as resolved. After an expected failure, **Check again** rechecks
the same root without restarting. It does not repair files. **Close** and the native close control
drain active work and exit. See the
[installation contract](architecture/launcher-managed-installation.md) for lifecycle and smoke
checks.

Opening the launcher again activates the existing instance for the same OS user, including when it
was started from another installation path. See the
[instance smoke check](architecture/launcher-instances.md#verification).

[Display preferences](architecture/launcher-settings.md) persist independently of installation
readiness. Trusted update/repair and controlled client startup are not implemented. Realm selection,
native AccountServer login and game-session handoff belong to Client/Networking.

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
- launcher tests verify launcher lifecycle, release verification, and product-boundary behavior;
- product-tool tests verify release metadata, signature-envelope validation, and managed-product
  staging;
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
8. creation of an ephemeral CI P-256 release authority;
9. deterministic release-manifest creation for the exact client publish;
10. external signing and Product Tool verification of the release-signature envelope;
11. launcher publication with the CI public release trust embedded;
12. absence of mutable installed-product layout, loose release metadata, and game content from the
    raw launcher publish;
13. authenticated managed-product composition using the manifest and signature;
14. absence of CI publisher key material from the composed product; and
15. Windows and macOS solution build/test coverage.

The CI release publisher is intentionally ephemeral and exists only to prove the complete pipeline.
It is not a production signing identity.

Publishing and signed product composition are treated as separate product guarantees from
compilation.

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
  /tmp/openconquer-release \
  /tmp/openconquer-managed

mkdir -p /tmp/openconquer-release

case "$(uname -s):$(uname -m)" in
  Darwin:arm64)
    TARGET_RUNTIME=osx-arm64
    ;;
  Darwin:x86_64)
    TARGET_RUNTIME=osx-x64
    ;;
  Linux:x86_64)
    TARGET_RUNTIME=linux-x64
    ;;
  Linux:aarch64|Linux:arm64)
    TARGET_RUNTIME=linux-arm64
    ;;
  *)
    echo "Unsupported local release runtime: $(uname -s):$(uname -m)" >&2
    exit 1
    ;;
esac

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

dotnet run \
  --project tools/OpenConquer.Product.Tool \
  --configuration Release \
  --no-build \
  --no-restore \
  -- \
  create-release-manifest \
  --client-publish /tmp/openconquer-client-publish \
  --target-runtime "$TARGET_RUNTIME" \
  --release-version dev-local \
  --release-sequence 1 \
  --minimum-launcher-version 1 \
  --output /tmp/openconquer-release/openconquer.release.json

umask 077

openssl ecparam \
  -name prime256v1 \
  -genkey \
  -noout \
  -out /tmp/openconquer-release/publisher-private.pem

openssl pkey \
  -in /tmp/openconquer-release/publisher-private.pem \
  -pubout \
  -outform DER \
  -out /tmp/openconquer-release/publisher-public.der

openssl dgst \
  -sha256 \
  -sign /tmp/openconquer-release/publisher-private.pem \
  -out /tmp/openconquer-release/openconquer.release.der \
  /tmp/openconquer-release/openconquer.release.json

dotnet run \
  --project tools/OpenConquer.Product.Tool \
  --configuration Release \
  --no-build \
  --no-restore \
  -- \
  create-release-signature \
  --release-manifest /tmp/openconquer-release/openconquer.release.json \
  --public-key /tmp/openconquer-release/publisher-public.der \
  --signature /tmp/openconquer-release/openconquer.release.der \
  --output /tmp/openconquer-release/openconquer.release.sig

PUBLIC_KEY="$(
  openssl base64 \
    -A \
    -in /tmp/openconquer-release/publisher-public.der
)"

printf \
  '{"schemaVersion":1,"keys":[{"algorithm":"ecdsa-p256-sha256-der","publicKey":"%s"}]}\n' \
  "$PUBLIC_KEY" \
  > /tmp/openconquer-release/release-trust.json

dotnet publish \
  src/OpenConquer.Launcher/OpenConquer.Launcher.csproj \
  --configuration Release \
  --no-restore \
  --output /tmp/openconquer-launcher-publish \
  -p:OpenConquerReleaseTrustPath=/tmp/openconquer-release/release-trust.json

test ! -e /tmp/openconquer-launcher-publish/openconquer.installation.json
test ! -e /tmp/openconquer-launcher-publish/openconquer.release.json
test ! -e /tmp/openconquer-launcher-publish/openconquer.release.sig
test ! -e /tmp/openconquer-launcher-publish/release-trust.json
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
  --release-manifest /tmp/openconquer-release/openconquer.release.json \
  --release-signature /tmp/openconquer-release/openconquer.release.sig \
  --output /tmp/openconquer-managed

test -f /tmp/openconquer-managed/openconquer.installation.json
test -f /tmp/openconquer-managed/openconquer.release.json
test -f /tmp/openconquer-managed/openconquer.release.sig
test -d /tmp/openconquer-managed/client
test -f /tmp/openconquer-managed/client/content/retail-5517/manifest.json

cmp \
  /tmp/openconquer-release/openconquer.release.json \
  /tmp/openconquer-managed/openconquer.release.json

cmp \
  /tmp/openconquer-release/openconquer.release.sig \
  /tmp/openconquer-managed/openconquer.release.sig

test ! -e /tmp/openconquer-managed/publisher-private.pem
test ! -e /tmp/openconquer-managed/publisher-public.der
test ! -e /tmp/openconquer-managed/release-trust.json

rm -f /tmp/openconquer-release/publisher-private.pem

git diff --check
```

Both `find` commands must print nothing.

Commit only after the implementation, tests, documentation, signed publish/composition boundaries,
and applicable platform verification for the current work slice are clean.
