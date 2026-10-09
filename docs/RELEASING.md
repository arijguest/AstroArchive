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
It stages the installer, compatibility alias, SHA-256 checksums and `update.json`
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
`signatures.json` and binds the final installer bytes to the alias, checksums and
update feed. `Uninstall.exe` uses the setup's signed bytes.

For manual staged builds, run `scripts/build-release.ps1 -Stage Prepare`, sign the
prepared app/launcher, run `-Stage Package -RequireSigned -ExpectedPublisher '<subject>'`,
sign setup, then run `-Stage Finalize` with the same signing options. Without staged
signing, the script produces an unsigned local build.

For user-facing Windows policy troubleshooting, see [installation and updates](UPDATES.md#smartscreen-and-application-control).
