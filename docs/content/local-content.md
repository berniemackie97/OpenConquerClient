# Local game content

The repository no longer tracks `content/`. Keep your existing local
`content/retail-5517/manifest.json` and `content/retail-5517/payload/` in place.
The client copies that set into local build/publish output when the manifest exists.
A clean source checkout builds without it; running the game requires real assets.

To create a set, use the existing importer with an original client installation and a new destination:

```sh
dotnet run --project tools/OpenConquer.Content.Tool -c Release -- \
  import-client-content --source /path/to/original-client --destination /path/to/new-content-set
```

Verify before moving it to `content/retail-5517`:

```sh
dotnet run --project tools/OpenConquer.Content.Tool -c Release -- \
  verify-content-set --content-set /path/to/new-content-set
```

Do not overwrite your current set without retaining a backup. No replacement or download source
is configured by this change. Source-only CI retains build, unit-test, synthetic OpenGL, publish
and redistribution-notice checks. Real asset map, release-package and installation gates require
private asset provisioning and are explicitly reported as unrun when the set is absent.
Previously published assets remain in Git history; this change does not rewrite it.
