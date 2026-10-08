# Installation and updates

Requires Windows 10 or 11 x64 and .NET Framework 4.8.

## Install, repair or uninstall

Download the installer from [the latest release](https://github.com/arijguest/AstroArchive/releases/latest).
New installations default to `%LOCALAPPDATA%\Programs\AstroArchive` and require
no administrator access there. Updates use the existing registered location.
Choose a local application folder for a custom installation.

Run the same package again to repair application files. Older packages cannot
replace newer ones. Uninstall through Windows Installed Apps. Repositories,
images, settings and local codecs are retained.

## Update

The desktop and Start menu launcher checks for stable releases. Choose **Install release**
or **Later** to open the current version. Inside the app, use
**Settings → Check for and install releases**.

The installer downloads with progress, is verified, and runs after AstroArchive
saves state and closes. The app restarts after setup. Wait for active work to finish
and close other instances of the installation before updating.

Manual installation and repair work offline. To open without a startup check,
run `Start.exe --no-updates`, or open `AstroArchive.exe` directly.
Updating from a portable copy installs into the registered/default installation
and retains the portable copy. Release checks need no GitHub account.

## Verify a download

Each release includes a `.sha256` file. In PowerShell, replace the filename with
your downloaded installer:

```powershell
Get-FileHash .\AstroArchive1.12.0.1.exe -Algorithm SHA256
```

Compare its Hash with the matching checksum file. A checksum verifies the bytes;
check the release notes and Windows file signature for publisher signing status.

## Troubleshooting

- **Download or update failure:** check internet access, free space and permissions.
  The current installation remains available; retry or run the downloaded installer manually.
- **Setup failure:** reopen the app from its shortcut or rerun setup. Include the
  exact error when reporting a problem.
- **Missing files or settings:** confirm the repository and installation location.
  Keep the whole repository, including `.astroarchive`, together during moves/backups.

Logs:

| Failure | Location |
| --- | --- |
| Setup | `%TEMP%\AstroArchive-setup-error.txt` |
| Release checks / installer launch | `%LOCALAPPDATA%\AstroArchive\updates\last-error.txt` |

## SmartScreen and Application Control

A SmartScreen reputation warning and **“An Application Control policy has blocked
this file”** have different causes. Check the package's signing status. Signing
identifies the publisher but does not guarantee acceptance by every Windows policy.

On a personal PC, inspect Windows Security → App & browser control. On a managed
PC, provide the error and log to the administrator for publisher approval.
Code Integrity events under Event Viewer → Applications and Services Logs →
Microsoft → Windows → CodeIntegrity → Operational can identify the blocked file.

See [Microsoft's Smart App Control documentation](https://learn.microsoft.com/windows/apps/develop/smart-app-control/overview).
Report app issues with the package version and exact error at
[GitHub Issues](https://github.com/arijguest/AstroArchive/issues).
