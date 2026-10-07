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
disabled it. Application code signing is not currently configured.

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
