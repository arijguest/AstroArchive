# Build and publish a Windows release

Pull requests validate without publishing. A push to `main` publishes an unissued
package version after the Windows checks pass. The workflow can also run manually
on `main`, or from a matching `v<package-version>` tag. Published releases are retained;
use a new package version for corrections.

## Set the version

The package version combines `application_version` and `installer_revision` from
`Installer/release.json`, for example `1.12.0` plus revision `1` gives `1.12.0.1`.

- **Application change:** increase the three-part application version and reset
  revision to 1. Set `AssemblyVersion` to `<app>.0`, `AssemblyFileVersion` to
  `<app>.<revision>`, and update the application manifest.
- **Installer/launcher change:** keep the application version, increase revision,
  and align `AssemblyFileVersion` with the package version.
- Update the bundled guide/version text and `Installer/Payload/Release_Notes.txt`.
  Keep the notes limited to the package being built.

## Validate and publish

Run `scripts/build-release.ps1` on a disposable Windows test account. It runs
engine, Windows codec, installer/updater, UI and install/repair/uninstall checks.
It also checks that Windows starts the installed launcher and app, simulates a
blocked update to confirm the automatic rollback to the earlier version, and fails
if Windows Code Integrity logged a block for any installed executable.

Unsigned builds can pass these checks on the runner but still be blocked on users'
PCs by Smart App Control, which judges new unsigned files by reputation. The
rollback protects users in that case. Only publisher signing prevents the block.
See [development](DEVELOPMENT.md) for isolated commands.

For v3 network changes, also run the remote transport and `--remote-only` UI
checks in [development](DEVELOPMENT.md#network-and-live-import-checks). The regular
release script does not run these extra fixtures. The [v3 validation workflow](../.github/workflows/v3.yml)
runs them and produces an unsigned development portable ZIP and installer;
it does not publish a GitHub release or modify the update feed.

Package the complete application output, including the separate `SMBLibrary.dll`
and its matching source/licence notices. The installer build bundles these files
alongside the guide and application notices. See [SMBLibrary distribution terms](../Application_Source/Remote/lib/README.md).

The workflow uses `GITHUB_TOKEN` with `contents: write` in the publishing job.
It stages one hybrid installer, its SHA-256 checksum and `update.json`
in a draft, verifies them, then makes the completed release latest.

When signing is disabled, packages are unsigned and the release notes say so.
When enabled, app, launcher and installer signatures must verify; signed builds
cannot fall back to unsigned publication.

## Configure Windows signing

Use [Microsoft Artifact Signing](https://learn.microsoft.com/azure/artifact-signing/overview)
with a validated **Public Trust** certificate profile. Grant the Azure application
**Artifact Signing Certificate Profile Signer** access to the profile.

Configure GitHub OIDC federated credentials for
`repo:arijguest/AstroArchive:ref:refs/heads/main`, and tag credentials if publishing
from tags. The issuer is `https://token.actions.githubusercontent.com`; the audience
is `api://AzureADTokenExchange`. Pull requests do not authenticate for signing.
See the action's [OIDC setup](https://github.com/Azure/artifact-signing-action/blob/main/docs/OIDC.md).

Set repository variables under GitHub Settings → Secrets and variables → Actions:

| Variable | Value |
| --- | --- |
| `AZURE_CLIENT_ID` | Azure application's client ID |
| `AZURE_TENANT_ID` | Azure tenant ID |
| `AZURE_SUBSCRIPTION_ID` | Signing account subscription |
| `ASTROARCHIVE_SIGNING_ENDPOINT` | Regional HTTPS endpoint |
| `ASTROARCHIVE_SIGNING_ACCOUNT` | Signing account name |
| `ASTROARCHIVE_SIGNING_PROFILE` | Public Trust profile name |
| `ASTROARCHIVE_SIGNING_PUBLISHER` | Exact certificate subject |
| `ASTROARCHIVE_SIGNING_ENABLED` | `true` when configured |

No client secret or private signing key is stored in the repository.
Use the subject returned by `(Get-AuthenticodeSignature .\test.exe).SignerCertificate.Subject`
for a file signed by the profile. Use the validated publisher consistently.

The workflow signs app/launcher before embedding their hashes, signs setup after
compilation, and verifies publisher and timestamp on all three files. It produces
`signatures.json` and binds the final installer bytes to the checksum and
update feed. `Uninstall.exe` uses the setup's signed bytes.

For manual staged builds, run `scripts/build-release.ps1 -Stage Prepare`, sign the
prepared app/launcher, run `-Stage Package -RequireSigned -ExpectedPublisher '<subject>'`,
sign setup, then run `-Stage Finalize` with the same signing options. Without staged
signing, the script produces an unsigned local build.

For user-facing Windows policy troubleshooting, see [installation and updates](UPDATES.md#smartscreen-and-application-control).

To publish a prerelease from a development branch, manually run **Windows build and release** on that branch with **prerelease** enabled. It runs the same package validation, publishes the matching package tag as a prerelease, and leaves the latest stable release unchanged. Do not push a version tag first: tag pushes use the stable publication path.

## Optional signing in 3.2.7

`Installer/release.json` explicitly sets `require_signing: false` for 3.2.7.
The release may publish checksum-verified unsigned output when signing is not
configured. The update feed and release notes record the signing status.

Publisher signing remains available through the Microsoft Artifact Signing setup
above. When used, the workflow verifies trusted RSA certificates, publisher,
timestamps and final hashes for the application, launcher and installer.

A future package can opt into mandatory signing by setting `require_signing: true`.
That opt-in stops publication when signing is disabled, and the release verifier
rejects unsigned output even with `--allow-unsigned`. The opt-in does not apply to
3.2.7. Pull-request validation may use unsigned artifacts independently.

Smart App Control evaluates each new unsigned file by hash. An older version's
acceptance does not transfer to a newly rebuilt package. Publishing unsigned
therefore cannot guarantee that Windows will allow installation or launch.
See [Microsoft's reputation explanation](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation).

## Single hybrid installer from 3.2.9

Build `AstroArchive3.2.9.1.exe` once. Both `url` and `download_url` in the
schema-1 feed point to that concise asset. The publication gate rejects extra
executables, including the retired `*-Offline-Setup.exe` alias. Reused staging
folders have generated offline aliases and their checksums removed. No historical
release is deleted or republished by this workflow.

The reviewed 3.2.9 branch baseline already accepts concise feed URLs and
`download_url`; installer tests exercise the 3.2.8.1 → 3.2.9.1 path. Very old
1.2.0 launchers only accept the retired alias: install the current single installer
manually to bridge those installations. Archive files and user preferences stay
outside versioned application directories.

`hybrid.yml` validates pushes to branch `3.2.9` without publishing a release. It
checks identities, rights, cache/network/orbit engines, seven-language WPF layouts,
and setup rendering. Inspect its screenshots before publication. Native Windows
validation remains necessary for per-monitor DPI and actual installation/launch.
