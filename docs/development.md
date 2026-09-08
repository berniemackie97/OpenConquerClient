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

`OpenConquer.Launcher` and `OpenConquer.Client` publish independently and are composed into one
authenticated managed product.

A managed product requires:

- an independently published client;
- a release manifest describing the exact client byte tree;
- an ECDSA P-256/SHA-256 signature over the exact manifest bytes;
- a validated release-signature envelope;
- an independently published launcher with corresponding publisher trust embedded; and
- final product composition through `OpenConquer.Product.Tool`.

Production release signing keeps private-key custody outside `OpenConquer.Product.Tool`. Local
development uses a separate persistent development-only publisher owned by the Product Tool.

### Local managed product

Create or refresh the canonical local managed product with:

```bash
dotnet run \
  --project tools/OpenConquer.Product.Tool \
  --configuration Release \
  -- \
  create-local-product
```

`create-local-product` intentionally accepts no options. It cannot accept a private key, arbitrary
publisher, target runtime, output root, release sequence, or minimum launcher version.

The command:

1. discovers the OpenConquer Client repository;
2. detects the current supported host runtime;
3. acquires the user-scoped local-product build lock;
4. loads or creates the persistent development publisher;
5. restores locked dependencies;
6. verifies the tracked `retail-5517` content set;
7. publishes the client;
8. verifies the published `retail-5517` content set;
9. rejects runtime `Server.dat`;
10. derives a release-sequence floor from the active and previous local products;
11. durably reserves the next development release sequence;
12. creates the release manifest;
13. signs the exact bounded manifest bytes with the development publisher;
14. creates the validated release-signature envelope;
15. creates launcher trust from the development public key;
16. publishes the launcher with that trust embedded;
17. validates raw-launcher isolation;
18. stages the authenticated managed-product candidate;
19. rejects loose development/signing state from the candidate;
20. promotes the candidate through the rollback-safe local activation boundary; and
21. performs best-effort cleanup of the isolated per-run workspace.

Supported runtime identities are:

```text
win-x64
win-arm64
osx-x64
osx-arm64
linux-x64
linux-arm64
```

The repository-local layout is:

```text
artifacts/local-product/
├── work/
├── product/
└── product.previous/
```

`product/` is the current active local managed product. After a subsequent successful activation,
the former active product is retained at `product.previous/`.

`work/` owns isolated per-run client, launcher, release, and candidate outputs. Successful builds
normally leave it empty. Workspace cleanup is best-effort and cleanup failure does not replace the
primary build result.

`artifacts/` is development output and is not source-controlled.

The persistent development publisher and coordination state live outside the repository:

```text
Windows: %LOCALAPPDATA%\OpenConquer\Development
macOS:   ~/Library/Application Support/OpenConquer/Development
Linux:   $XDG_CONFIG_HOME/OpenConquer/Development
         or ~/.config/OpenConquer/Development
```

The exact platform location is derived from the current user's platform configuration directories.
The state contains the development-only P-256 private key, release-sequence state, and coordination
locks. The development private key is PKCS#8 and never enters repository-local release artifacts or
the final managed product.

On Unix, development-state directories and sensitive files are restricted to the current user.

The user-scoped build lock serializes composition across repository clones that share the same
development publisher state.

Local release sequences are monotonically increasing and are never intentionally reused. Sequence
gaps are valid if a build fails after a sequence has been durably reserved. If persistent sequence
state is lost, the greatest sequence in the active or previous local product provides an
anti-rollback floor for the next reservation.

Client publication, content verification, and `Server.dat` rejection occur before sequence
reservation. Failures at those earlier boundaries therefore do not consume a release sequence.

A candidate is fully staged and validated before activation mutates the active product. Activation
rejects release rollback and preserves the previous active product as the rollback slot. If the
final promotion fails after moving the active product aside, activation attempts to restore the
previous product.

### Run the local launcher

On macOS/Linux:

```bash
./artifacts/local-product/product/OpenConquer.Launcher
```

On Windows:

```powershell
.\artifacts\local-product\product\OpenConquer.Launcher.exe
```

The launcher resolves the managed installation from its own `AppContext.BaseDirectory`.

A correctly authenticated local product reports:

```text
OpenConquer is ready
Release local-N is verified for this device.
```

Resolution verifies:

- installation schema and layout;
- release-manifest structure and compatibility;
- embedded publisher trust;
- the ECDSA P-256/SHA-256 release signature;
- target runtime;
- exact client file membership;
- client file lengths;
- SHA-256 hashes;
- valid package paths; and
- the platform-specific client executable; and
- portable executable permissions on Unix.

A raw launcher build or publish is not an installed OpenConquer product and does not contain a
managed client component. A launcher published without corresponding embedded release trust fails
closed when resolving a signed release.

After an expected installation failure, **Check again** rechecks the same root without restarting.
It does not repair files.

The trusted client-generation update/repair and explicit verified-rollback transaction is
implemented below the UI boundary. Authoritative release discovery/acquisition, launcher self-update,
player-facing maintenance orchestration, and controlled client startup remain future launcher
capabilities. Realm selection, native AccountServer login, and game-session handoff belong to
Client/Networking.

### Tamper smoke check

When changing managed-product integrity or installation resolution, exercise a real launcher against
an isolated modified product rather than mutating the canonical active product.

On macOS:

```bash
tmp_root="$(mktemp -d /tmp/openconquer-corrupt.XXXXXX)"
active_release="$(jq -r '.activeRelease' artifacts/local-product/product/openconquer.installation.json)"
client_path="releases/$active_release/client/OpenConquer.Client"
original_hash="$(shasum -a 256 "artifacts/local-product/product/$client_path" | awk '{print $1}')"

ditto artifacts/local-product/product "$tmp_root/product"
printf '\0' >> "$tmp_root/product/$client_path"

"$tmp_root/product/OpenConquer.Launcher"
launcher_exit=$?

after_hash="$(shasum -a 256 "artifacts/local-product/product/$client_path" | awk '{print $1}')"

echo "launcher_exit=$launcher_exit"
echo "active_product_unchanged=$([[ "$original_hash" == "$after_hash" ]] && echo yes || echo no)"

rm -rf -- "$tmp_root"
```

The modified copy must not reach the ready state. A changed authenticated client file reports an
installation-integrity failure:

```text
Installation needs repair
OpenConquer game files are missing, changed, or unexpected.
```

`launcher_exit=0` after normal user shutdown is expected; the launcher itself operated correctly.
The security assertion is that the tampered installation was rejected.

`active_product_unchanged=yes` confirms that the smoke test did not mutate the canonical active
product.

### Rider workflow

Rider configuration is developer-local and is not committed to the repository.

Keep direct client and launcher run configurations for normal inner-loop development.

Create a run configuration named:

```text
Create Local Product
```

Configure it to run `tools/OpenConquer.Product.Tool/OpenConquer.Product.Tool.csproj` in `Release`
with program arguments:

```text
create-local-product
```

and the repository root as its working directory.

Create a second configuration named:

```text
OpenConquer - Local Product
```

that launches the canonical managed-product executable:

```text
macOS/Linux:
$PROJECT_DIR$/artifacts/local-product/product/OpenConquer.Launcher

Windows:
$PROJECT_DIR$\artifacts\local-product\product\OpenConquer.Launcher.exe
```

Configure **Create Local Product** as a Before Launch task for **OpenConquer - Local Product**.

This keeps direct project execution available for fast inner-loop work while making the
authenticated managed product the normal product-boundary acceptance path.

### Release composition primitives

`create-local-product` is development-only orchestration. The underlying Product Tool commands
remain separate because production publishing must keep release identity and private signing
authority outside the Product Tool.

Create the manifest for an independently published client:

```bash
dotnet run \
  --project tools/OpenConquer.Product.Tool \
  --configuration Release \
  -- \
  create-release-manifest \
  --client-publish /path/to/client-publish \
  --target-runtime osx-arm64 \
  --release-version 1.0.0 \
  --release-sequence 1 \
  --minimum-launcher-version 1 \
  --output /path/to/release/openconquer.release.json
```

Create launcher trust from one or more validated public publisher keys:

```bash
dotnet run \
  --project tools/OpenConquer.Product.Tool \
  --configuration Release \
  -- \
  create-release-trust \
  --public-key /path/to/publisher-public.der \
  --output /path/to/release/release-trust.json
```

`--public-key` may be repeated for additional trusted publishers within the Product Tool's bounded
trust-key limit.

Production signing occurs externally. Supply the exact release manifest to the controlled production
signing process, then give the resulting DER ECDSA signature and corresponding public key to the
Product Tool:

```bash
dotnet run \
  --project tools/OpenConquer.Product.Tool \
  --configuration Release \
  -- \
  create-release-signature \
  --release-manifest /path/to/release/openconquer.release.json \
  --public-key /path/to/publisher-public.der \
  --signature /path/to/release/openconquer.release.der \
  --output /path/to/release/openconquer.release.sig
```

Publish the launcher with the trust document embedded:

```bash
dotnet publish \
  src/OpenConquer.Launcher/OpenConquer.Launcher.csproj \
  --configuration Release \
  --no-restore \
  --output /path/to/launcher-publish \
  -p:OpenConquerReleaseTrustPath=/path/to/release/release-trust.json
```

Compose the managed product:

```bash
dotnet run \
  --project tools/OpenConquer.Product.Tool \
  --configuration Release \
  -- \
  stage-managed-product \
  --launcher-publish /path/to/launcher-publish \
  --client-publish /path/to/client-publish \
  --release-manifest /path/to/release/openconquer.release.json \
  --release-signature /path/to/release/openconquer.release.sig \
  --output /path/to/managed-product
```

Publisher trust is embedded into the launcher assembly through `OpenConquerReleaseTrustPath`; it is
not shipped as mutable loose `release-trust.json`.

The Product Tool validates the release inputs and managed composition boundaries but never accepts
or owns a production private signing key.

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
  --content-set /path/to/client-publish/content/retail-5517
```

Production and local managed client publishes must not contain `Server.dat`.

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
- launcher tests verify launcher lifecycle, release verification, display settings, and
  product-boundary behavior;
- product-tool tests verify release metadata, publisher trust, development identity/state,
  local-product orchestration, artifact boundaries, activation, and managed-product staging;
- platform tests verify desktop host mechanics; and
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
10. release-trust creation through the Product Tool;
11. external signing and Product Tool verification of the release-signature envelope;
12. launcher publication with CI public release trust embedded;
13. absence of mutable installed-product layout, loose release metadata, and game content from the
    raw launcher publish;
14. authenticated managed-product composition using the manifest and signature;
15. absence of CI publisher key material from the composed product; and
16. Windows and macOS solution build/test coverage.

The CI release publisher is intentionally ephemeral and exists only to prove the complete release
pipeline. It is unrelated to the persistent local-development publisher and is not a production
signing identity.

Publishing and authenticated product composition are treated as separate product guarantees from
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

dotnet run \
  --project tools/OpenConquer.Product.Tool \
  --configuration Release \
  -- \
  create-local-product

git diff --check
```

For changes that affect launcher resolution, release integrity, product composition, activation, or
managed-product layout, also run the canonical managed launcher and the applicable tamper smoke
check.

Commit only after the implementation, tests, documentation, authenticated product composition,
applicable native desktop verification, and strict re-audit for the current work slice are clean.
