# Publishing a Windows release

GitHub Actions tests and builds each change. A push to `main` automatically
publishes a release when `Installer/release.json` names a package not already
published. Pull requests build without publishing. You can also run the Windows
workflow manually on `main`, or push a matching four-part `v<package>` tag.

## Version changes

- **Installer or launcher change:** keep `application_version` and increase
  `installer_revision` in `Installer/release.json`.
- **Application change:** set a new three-part version in both assembly attributes
  in `Application_Source/AssemblyInfo.cs` and `application_version` in
  `Installer/release.json`; reset `installer_revision` to 1. Update the application
  manifest version and user-visible version text as appropriate.
- Update `Installer/Payload/Release_Notes.txt` before committing.

For example, application 1.2.0 with installer revision 2 is package **1.2.0.2**
and release tag **v1.2.0.2**. Increasing the revision to 3 publishes v1.2.0.3;
installed launchers offer that package even though the app version is unchanged.

The workflow runs application tests, installer/update tests, application and
installer builds, real Windows install/repair/uninstall smoke checks and a WPF
render smoke for both the application and installer. Only a successful build can publish. Release assets are uploaded
into a draft before publication: the concise `AstroArchive<package>.exe` installer, a compatibility alias for
1.2.0 launchers, their SHA-256 checksums and
`update.json`. Publishing makes the completed release the latest stable version.
Published releases are not overwritten; increment the version for corrections.
The original supplied 1.2.0.1 offline installer is also published as a historical
release, without making it latest.

The workflow uses GitHub's built-in `GITHUB_TOKEN` with `contents: write` only in
the publishing job. No personal access token or third-party release service is
required. Enable GitHub Actions for the repository if organisational policy has
disabled it. New public releases require trusted, timestamped Authenticode
signatures from the configured publisher. Pull requests and local builds can
still build unsigned executables; an unsigned artifact cannot pass publication.

## Configure Windows signing

The workflow supports [Microsoft Artifact Signing](https://learn.microsoft.com/azure/artifact-signing/overview)
(formerly Trusted Signing) with GitHub OIDC authentication. The service is paid
and requires publisher identity validation; check its current eligibility for
your country and individual or organisation account. Create a **Public Trust**
certificate profile, complete identity validation and grant the Azure application
the **Artifact Signing Certificate Profile Signer** role on that profile.
The workflow does not store or export a signing private key.

Configure the Azure application's federated credentials for this repository's
`main` branch (`repo:arijguest/AstroArchive:ref:refs/heads/main`). Add separate
credentials for release tags if using tag-triggered releases. Use GitHub's OIDC
issuer `https://token.actions.githubusercontent.com` and audience
`api://AzureADTokenExchange`. See the action's [OIDC setup](https://github.com/Azure/artifact-signing-action/blob/main/docs/OIDC.md).
Pull requests never authenticate to the signing service.

Set these GitHub Actions **repository variables** under Settings → Secrets and
variables → Actions → Variables:

| Variable | Value |
| --- | --- |
| `AZURE_CLIENT_ID` | Azure application's client ID |
| `AZURE_TENANT_ID` | Azure tenant ID |
| `AZURE_SUBSCRIPTION_ID` | Subscription containing the signing account |
| `ASTROARCHIVE_SIGNING_ENDPOINT` | Account's regional HTTPS signing endpoint |
| `ASTROARCHIVE_SIGNING_ACCOUNT` | Signing account name |
| `ASTROARCHIVE_SIGNING_PROFILE` | Public Trust certificate profile name |
| `ASTROARCHIVE_SIGNING_PUBLISHER` | Exact certificate subject, including CN/O/C fields, as issued for the validated identity |
| `ASTROARCHIVE_SIGNING_ENABLED` | `true`, after the account/profile and OIDC are ready |

No Azure client secret is required. Obtain the exact publisher subject from a
test file signed with your profile (`(Get-AuthenticodeSignature .\test.exe).SignerCertificate.Subject`).
Do not use a self-signed certificate; ordinary user machines will not trust it.
Use the validated publisher identity consistently across future releases.

The build prepares the application and launcher, signs both with SHA-256 and an
RFC3161 timestamp, verifies their trusted signatures and publisher, then embeds
their signed bytes and hashes. It signs the compiled setup next. Finalization
verifies all three signatures, refreshes installer checksums, copies the signed
compatibility alias, generates the update feed and verifies the installed app,
launcher and uninstaller during the Windows smoke test. `Uninstall.exe` is a
byte-for-byte copy of the signed setup. The publication job checks
`signatures.json` against the final installer bytes, alias, checksums and feed.
It fails before creating or publishing a new release if signing is missing.

After setup, publish a **new package version**; existing unsigned published
assets are retained. For an installer revision, also align the fourth part of
`AssemblyFileVersion` in `Application_Source/AssemblyInfo.cs` with that revision,
as the builder requires matching file versions. The staged commands are
`scripts/build-release.ps1 -Stage Prepare`, `-Stage Package -RequireSigned
-ExpectedPublisher '<subject>'`, and `-Stage Finalize -RequireSigned
-ExpectedPublisher '<subject>'`, with your signing tool run between stages.
Running the script without stages preserves the unsigned local build path.

## SmartScreen and Application Control

SmartScreen reputation warnings and **“An Application Control policy has
blocked this file”** have different causes. Signing establishes publisher
identity and helps Windows evaluate releases, but it cannot guarantee that
SmartScreen warnings disappear immediately or satisfy every managed policy.
Microsoft's [file submission portal](https://www.microsoft.com/wdsi/filesubmission)
can review a release that is incorrectly detected; submit the exact published
installer as the developer and retain its SHA-256 for the report.

An Application Control block may come from Windows Smart App Control or an
organisation's App Control for Business policy. The installer reports policy
errors without changing those settings. Read `%TEMP%\AstroArchive-setup-error.txt`
for the exception, rejected executable (for process launch failures) and native
error code. In Event Viewer, inspect **Applications and Services Logs →
Microsoft → Windows → CodeIntegrity → Operational** at the failure time
(especially event 3077, and related 3089 signature details). An administrator
can use those events to identify the rejected file and approve the required
publisher under a managed policy. If the event names another dependency or
component, address that file too; the installer dialog alone does not identify
the blocked file. Settings → Privacy & security → Windows Security → App &
browser control shows whether Smart App Control is enforcing on a personal PC.

## Local validation

Run `scripts/build-release.ps1` in Windows PowerShell 5.1 on a **test account**:
the smoke test temporarily registers AstroArchive and creates shortcuts. It
installs in a generated temporary folder and uninstalls after validation.
Do not run that registration smoke test alongside your everyday installation.
For isolated engine tests, run `Application_Source/test.ps1` and
`Installer/test.ps1` separately.

The original `Validation.txt`, `Build_Validation.txt` and binary validation files
are historical reports supplied with the upload. Current CI results are the
source of truth for new builds. Linux engine tests cannot establish that Windows
UI, registry, shortcut or update prompt behavior works.
