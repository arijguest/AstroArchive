# AstroArchive Windows installer

Application 1.2.0, installer package 1.2.0.2, Windows 10/11 x64 and .NET Framework 4.8 or later.

## Install, update or repair

Close AstroArchive and run `AstroArchive-1.2.0-Windows-x64-Offline-Setup.exe`.
The installer finds the per-user installation registered by the supplied
example. Use its existing folder to update it. New installations default to
`%LOCALAPPDATA%\Programs\AstroArchive`. Administrative elevation is not needed.
The Windows folder selector is used for a custom location; choose a local
application folder. A nonempty, unrelated folder is refused.

Desktop and Start menu shortcuts are created. Windows Installed Apps lists
AstroArchive with an uninstaller. Running the same package again repairs its
managed application files. Downgrades are refused using numeric versions.

The installed launcher checks the latest stable GitHub release at startup.
When a newer package is available it asks whether to install, with Yes selected
by default. No opens the current version. Accepted downloads are verified with
SHA-256, installed in place, and AstroArchive restarts. Failed or offline checks
leave the existing app available. Manual installation remains fully offline.
See [update details](../docs/UPDATES.md) and [release instructions](../docs/RELEASING.md).

The application remains version 1.2.0. The release workflow rebuilds it from source.
Its source is included separately in `Application_Source`. A portable executable
alone has no installation registration: installing this package creates a new
installation while retaining the app's existing per-user settings.

## Data preservation

Setup and uninstall do not clear repositories, images, app settings, SQLite
import manifests, plate-solving caches or API keys. Uninstall deletes only
validated, recorded application paths and shortcuts that point inside this
installation. Unrelated files and nonempty folders are retained. Clearing app
data remains an explicit action inside AstroArchive Settings.

Payload files have embedded SHA-256 hashes. Setup stages and verifies them
before activating the new version, writes the installation record atomically,
and restores the previous payload and record if the update fails. It refuses
linked installation paths and repairs that would discard additional files in
the package's application folder. Concurrent installers are serialized and a
running application must be closed. This is exception rollback, not a claim of
full recovery from power loss at every filesystem operation.

## Rebuild on Windows

From the `Installer` directory, run in Windows PowerShell 5.1:

```powershell
.\test.ps1
.\build.ps1
```

The included original prebuilt app is the default payload for an installer-only local build. The release workflow supplies a fresh source build. No SDK, Visual Studio,
third-party installer compiler or GitHub connection is required. The scripts
use the .NET Framework C# compiler shipped with Windows.

For a new application version, build `Application_Source\build.ps1` first or
supply another app executable, set `application_version` in `release.json` to
its three-part file version, and reset `installer_revision` to 1. For an
installer-only change, keep the application version and increment the revision.
Update release notes alongside the version. Example:

```powershell
.\build.ps1 -AppExecutable '..\Application_Source\dist\AstroArchive.exe'
```

The build checks that the app and package versions agree, compiles the update-aware
launcher, creates a fresh payload hash manifest, embeds all payloads, icons and
the Windows manifest, and emits the setup EXE and SHA-256 file into `dist`.
Use a newer package for an existing install. Application version remains visible
in Windows Installed Apps; the package revision is stored separately.

## Command line

```powershell
# Silent fresh install or upgrade at the registered/default location
.\AstroArchive-1.2.0-Windows-x64-Offline-Setup.exe --silent

# Update only; fail when the folder has no recognized installation
.\AstroArchive-1.2.0-Windows-x64-Offline-Setup.exe --update --silent --root 'C:\Users\Me\AppData\Local\Programs\AstroArchive'

# Uninstall this installation, retaining repository and settings data
.\AstroArchive-1.2.0-Windows-x64-Offline-Setup.exe --uninstall --root 'C:\Users\Me\AppData\Local\Programs\AstroArchive'
```

Exit code 0 means success; 1 means failure or an unfinished setup. `--restart`
launches the app after a successful silent installation. Failures are written
to `%TEMP%\AstroArchive-setup-error.txt`. Installed `Uninstall.exe` relaunches
from a temporary copy so the original can be removed after its process exits.

## Validation

The supplied `Build_Validation.txt` describes the original offline release. Current checks run in the Windows CI workflow and `scripts/build-release.ps1`.
The engine tests operate only on generated fixtures, with registry, shortcuts
and process checks replaced by test callbacks. Native Windows UI, shortcuts,
Installed Apps registration, app launch, and self-removing uninstall require
a Windows smoke test; they cannot be exercised in the Linux build environment.
The output is unsigned and has no embedded signing identity.

Recommended smoke test on a Windows test account:

1. Install fresh, launch from both shortcuts and confirm the app opens.
2. Create a small repository, change a setting, close the app and rerun setup.
3. Install using the attached example first, then run this offline installer;
   confirm its existing location is selected and the repository/settings persist.
4. Try an update while the installed app is open and confirm the close-app message.
5. Uninstall through Installed Apps; confirm application shortcuts are removed
   and the repository and settings remain. Run this installer again if desired.

This source package includes the app's existing catalogue notices and quick
start instructions. The installer introduces no additional runtime packages.
