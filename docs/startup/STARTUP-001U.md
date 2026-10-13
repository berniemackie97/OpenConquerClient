# STARTUP-001U — Local signed-realm builds and native-selection presentation cleanup

## Baseline and bounded goal

Applies after merged PR #100, main `58c1cac96e43adf1f3f7d687ac7fd693af50fcde`. The authenticated development realm at `https://localhost/realms.json` is already published by the existing offline P-256 signer and Caddy. This slice removes the need to supply `OpenConquerCatalogAuthorityFile` manually to every build **after a one-time explicit developer opt-in**, and removes non-native operational/selection prompts from the populated login artwork. It does not change signer custody, endpoints, signature validation, catalog expiration, the type-1060 client handshake, Enter activation, or any native asset pixels.

## One-time developer provisioning (macOS)

The client project conditionally imports `$HOME/.config/openconquer/client.local.props`. This file does not belong to the repository; it contains a path to the **public** `authority.json` and a local-only MSBuild marker. The import is skipped when `CI=true` or an explicit `OpenConquerCatalogAuthorityFile` property is supplied. The import is absent by default and never supplies an unsigned fallback. The normal `EmbeddedResource` and publisher trust boundary are unchanged.

From the repository root:

```bash
python3 tools/development/configure_local_realm.py
# Future builds in Terminal and Rider can then use ordinary commands:
dotnet build src/OpenConquer.Client -c Release
dotnet run --project src/OpenConquer.Client -c Release --no-build -- --window-size 1024x768
```

The configuration command validates the existing regular, non-symlinked public authority file, enforces the established loopback HTTPS catalog/signature URI pair, and creates the private user-owned MSBuild settings file exactly once. It does **not** read the signing private key or re-sign publications; it refuses to overwrite different local settings. Use `python3 tools/development/configure_local_realm.py --disable` to remove only a recognized generated settings file. `--authority` supports an explicitly selected existing public authority file, but only the same `https://localhost/realms.json` / `https://localhost/realms.sig` loopback deployment.

**Safety boundary:** automatic local authority imports are disabled for CI and explicitly prohibited for `dotnet publish`. To publish, provide an independently reviewed production authority explicitly with `-p:OpenConquerCatalogAuthorityFile=...` and ensure the resulting distribution was built from the intended trusted settings. Do not distribute a personal development authority. An ordinary `dotnet build -c Release` in the local checkout remains a developer build; Release is not the same as a production publisher authorization.

If Caddy is unavailable, the user certificate is untrusted, the catalog expired, the signature is wrong, or the publisher identity changes, the client still rejects the directory. Automatic builds are not a substitute for operating the HTTPS server and scheduled signed-catalog renewal.

## Authenticated selection UI contract

`LoginDirectoryStatus` still computes acquisition states without conflating missing authority, network failure, signed empty directories, or unselected regions/servers. Only `NotConfigured` is presented as the existing visual banner because it reflects a build/distribution configuration problem. `Checking`, `Unavailable`, `NoRegions`, `ChooseRegion`, `NoServers`, and `ChooseServer` are **not rendered** over the original selection artwork. The preexisting console diagnostic remains available for failed authenticated discovery; it does not affect unsigned endpoint authorization. The renderer now creates exactly one optional-status label texture rather than eagerly allocating all seven. Selection, focus, input, page controls and signed server artwork retain their established behavior.

This removes the dark `SELECT A REGION` box seen in the accepted-authenticated screenshot without inventing startup prompts or modifying bitmap pixels. Region/server selection still requires an explicit user action. On an unconfigured build the existing `REALM DIRECTORY NOT CONFIGURED` banner is intentionally retained.

## Verification

Run the Python provisioning tests, locked restore, formatting verification, Release build, focused and full client tests, retail content-set verification, and Windows/macOS CI. Locally verify that a regular `dotnet build` and Rider build embed the expected authority without command-line properties. Verify 1024×768 and 800×600 render stages: no selection prompt appears while a signed directory is available, and the verified retail region/server art, account-panel Y=620 and original actions remain unchanged. Verify fail-closed behavior for an absent issuer, an expired/mis-signed directory, and a failed Caddy connection. Verify that `dotnet publish` fails when using local opt-in and succeeds only with deliberate production trust configuration. Do not claim this slice restores original 5517 login Flash animation or registration/footer/cursor assets.
