# STARTUP-001U: Local signed-realm builds and selection screen cleanup

The signed development realm directory at `https://localhost/realms.json` is produced by the existing offline P-256 signer and served by Caddy. This note covers two changes. First, after a one-time opt-in, developers no longer have to pass `OpenConquerCatalogAuthorityFile` to every build. Second, the login screen no longer draws status and selection prompts over the populated artwork, since the retail client has no such prompts. Key custody, endpoints, signature checks, catalog expiry, the type-1060 client handshake, the disabled Enter button and the retail artwork pixels are unchanged. These changes do not restore the original 5517 login Flash animation or the registration, footer or cursor assets.

## One-time developer setup (macOS)

The client project imports `$HOME/.config/openconquer/client.local.props` when that file exists. The file lives outside the repository. It holds the path to the public `authority.json` and an MSBuild marker that flags the build as local-only. The import is skipped when `CI=true` or when an `OpenConquerCatalogAuthorityFile` property is already set. Nothing is imported by default, and the import never provides an unsigned fallback. The authority is still embedded as a normal `EmbeddedResource`, and publisher trust works as before.

From the repository root:

```bash
python3 tools/development/configure_local_realm.py
# Future builds in Terminal and Rider can then use ordinary commands:
dotnet build src/OpenConquer.Client -c Release
dotnet run --project src/OpenConquer.Client -c Release --no-build -- --window-size 1024x768
```

The script checks that the existing public authority file is a regular file (not a symlink) and that it names the expected loopback HTTPS catalog and signature URIs. It then creates the private, user-owned MSBuild settings file, once. It never reads the signing private key or re-signs anything, and it refuses to overwrite different local settings. `python3 tools/development/configure_local_realm.py --disable` removes the settings file, but only if it recognizes the file as one it generated. `--authority` selects a different existing public authority file, which must still point at the same loopback deployment: `https://localhost/realms.json` and `https://localhost/realms.sig`.

**The local import is for development only.** It is disabled in CI and blocked for `dotnet publish`. To publish, pass a separately reviewed production authority with `-p:OpenConquerCatalogAuthorityFile=...` and confirm that the distribution was built with the intended trusted settings. Do not distribute a personal development authority. A plain `dotnet build -c Release` in a local checkout is still a developer build. Building in Release does not give it production publisher trust.

The client still rejects the directory if Caddy is down, the user certificate is not trusted, the catalog has expired, the signature is wrong or the publisher identity has changed. The automatic import does not replace running the HTTPS server and renewing the signed catalog on a schedule.

## Selection screen status

`LoginDirectoryStatus` still keeps separate states for a missing authority, a network failure, a signed but empty directory, and no region or server being selected. Only `NotConfigured` is shown on screen, as the existing banner, because it means the build or distribution is misconfigured. `Checking`, `Unavailable`, `NoRegions`, `ChooseRegion`, `NoServers` and `ChooseServer` are no longer drawn over the original selection artwork. A failed fetch of the signed directory is still reported in the console, as before. That message never causes an unsigned endpoint to be accepted. The renderer now creates one texture for the optional status label instead of allocating all seven up front. Selection, focus, input, page controls and the signed server artwork behave as before.

This removes the dark `SELECT A REGION` box that appeared once a signed directory had been accepted. No startup prompts were added and no bitmap pixels changed. Regions and servers are still selected only when the user picks them. A build without a configured authority still shows the `REALM DIRECTORY NOT CONFIGURED` banner.

## Verification

Run the Python tests for the setup script, a locked restore, the formatting check, a Release build, the focused and full client tests, the retail content-set verification and Windows and macOS CI. Locally, check that a plain `dotnet build` and a Rider build both embed the expected authority without any command-line properties.

At both 1024x768 and 800x600, check that no selection prompt appears while a signed directory is available. The retail region and server art, the account panel at Y=620 and the original action buttons must be unchanged. Check that the client rejects the directory when the issuer is absent, when the directory is expired or wrongly signed, and when the Caddy connection fails. Check that `dotnet publish` fails with the local opt-in in place and succeeds only when a production trust configuration is passed in.
