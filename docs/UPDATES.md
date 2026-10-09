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

AstroArchive checks stable releases quietly shortly after startup and every six
hours while it is open. An overdue check runs after waking from sleep. Offline
checks retry after 15 minutes, one hour, then six hours without opening dialogs.

A new package appears in the **Activity bell** and **Guide → About AstroArchive**.
About shows both application and package versions, the last successful check,
release notes and **Check now**. Reading or dismissing the bell notification keeps
the available release visible in About. Package revisions also trigger notices.

**Settings → Updates** offers manual checks, release notes and **Install and
restart**. Automatic checks can be disabled there. Installation starts only when
you request it and active archive work has finished. Downloads appear in Activity
with progress and Cancel; closing Activity leaves the download running. The
verified installer runs after AstroArchive saves state and closes, then restarts
the app. A failed or canceled download leaves the current installation available.

Manual installation and repair work offline. To open without automatic checks,
run `Start.exe --no-updates` or `AstroArchive.exe --no-updates`. The launcher opens
the app immediately; its explicit `--updates` command still supports manual
checking and shares the app's validated release cache.
Updating from a portable copy installs into the registered/default installation
and retains the portable copy. Release checks need no GitHub account.

## Verify a download

Each release includes a `.sha256` file. In PowerShell, replace the filename with
your downloaded installer:

```powershell
Get-FileHash .\AstroArchive-VERSION.exe -Algorithm SHA256
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
