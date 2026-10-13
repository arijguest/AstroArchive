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

### If Windows blocks a new version

After every install, update or repair, setup checks that Windows will start the
new `Start.exe` and `AstroArchive.exe` before opening the app. Earlier versions
remain installed after an update. If Smart App Control, SmartScreen or an App
Control policy refuses the new version, setup automatically:

1. activates the newest earlier installed version that Windows starts,
2. points the desktop, Start menu and pinned shortcuts back to it,
3. opens it with a notice naming the blocked and restored packages, and
4. stops offering the blocked package as an update on that PC.

The launcher does the same if Windows later refuses the app itself. Repositories,
images and settings are never changed. The blocked package's files stay installed,
so a later repair or uninstall still manages them. If the blocked package starts
again later, for example after it is signed, reinstalling it reactivates it and
it is offered again.

If an installation is already stuck on a blocked version (for example 3.1.6),
run a newer installer, or run an installer with `--verify-launch`, to restore the
newest working version. Setup exits with code 3 when it restores an earlier
version and code 4 when no earlier installed version will start.

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

Setup and the launcher check whether Windows will start a new version and restore
the previous working version when it will not. See
[If Windows blocks a new version](#if-windows-blocks-a-new-version).

On a personal PC, inspect Windows Security → App & browser control. On a managed
PC, provide the error and log to the administrator for publisher approval.
Code Integrity events under Event Viewer → Applications and Services Logs →
Microsoft → Windows → CodeIntegrity → Operational can identify the blocked file.

See [Microsoft's Smart App Control documentation](https://learn.microsoft.com/windows/apps/develop/smart-app-control/overview).
Report app issues with the package version and exact error at
[GitHub Issues](https://github.com/arijguest/AstroArchive/issues).

## Online access and the 3.2.9 installer

Setup includes location and online-access steps. Hybrid is recommended; the
complete installation still works without an internet connection. Offline uses
bundled and previously downloaded information. Existing choices survive updates,
repair and reinstall. Silent installations and portable/older first launches ask
for a choice in the application; Pending blocks automatic internet access.

Preferences > General > Online access changes the mode and shows the information
cache limit, usage and clear action. Offline cancels enrichment/downloads and
suspends automatic release checks without changing the separately saved update
preference or the Astrometry.net solver choice. Local LAN telescope and Stellarium
connections continue to work. Internet actions ask you to reconnect through
Preferences; they do not switch modes automatically.

Only `AstroArchive3.2.9.1.exe` is produced. The Offline-Setup duplicate is retired.
Current updaters accept its concise name. An original 1.2.0 launcher needs a manual
installation of the current single installer. Historical releases remain available.

Successful install, update and repair write a one-time completion receipt. With
Open after install checked, setup closes after a successful launch; launch failure
leaves a retry action. With it unchecked, Finish stays visible and the next app
launch shows the receipt. A rollback shows recovery instead of success.
