# Windows updates

The installed `Start.exe` checks the latest stable release's `update.json` over
HTTPS. It offers a package only when its four-part version is newer and its
application version is not older. The startup dialog shows package notes and defaults to Install release; Later
opens the current version.

In the application, open **Settings → Check for and install new releases**.
Choose **Check for new releases**, then **Install release**. Installation follows
this flow automatically:

1. Download the installer under `<installation>\updates\<download-id>\` and verify
   its exact byte count and SHA-256. There is no save-location dialog.
2. Save current settings and checkpoint the archive index.
3. Reverify the saved installer, start it with the existing installation location
   and a process-wait argument, and shut down the application and settings dialogs.
4. Wait for both the application and launcher to exit, then stage and verify the
   new payload, activate its installation record atomically and update shortcuts.
5. Record a version-specific update receipt and restart AstroArchive with the
   startup update check skipped once. A dismissible banner confirms the installed
   package on the next launch and consumes its matching receipt once.

Downloads sit outside `app-<version>-r<revision>`, so they do not interfere with
same-package repair or get mistaken for managed application payloads. Verified
installers remain available for offline repair. The default installation is
`%LOCALAPPDATA%\Programs\AstroArchive`; custom installations use their own folder.
The application folder must be writable. A write/download/verification failure
leaves the running app open. A later setup failure preserves the previous package
through the existing rollback engine and displays its error. Reopen the app from
its shortcut or rerun the installer to retry.

A portable copy downloads under `updates` beside its executable and installs into
the registered/default managed installation. It retains the portable copy and
per-user settings. An existing registered installation is included in the version
comparison so a portable copy cannot offer a downgrade. The application's file
version includes the package revision, preventing an older published package
from being offered to a newer portable build.

Repositories, captures, manifests, deletion history and user settings are outside
managed application-file replacement. Active work must finish before opening
Settings. Another open AstroArchive instance for the target installation blocks
the handoff rather than losing its work.

## Verification and compatibility

The feed binds the application version, package revision, exact GitHub asset URL,
byte count and SHA-256. Only this repository's installer URL is accepted; HTTPS
redirects are restricted to GitHub's release hosts. Downloads have size and time
limits and keep TLS certificate verification enabled. Cached bytes are checked
again immediately before launch. The installer verifies its embedded payloads.
SHA-256 detects damaged or mismatched bytes; it is not a code-signing identity.

The feed retains the legacy `url` asset and adds `download_url` for the concise
`AstroArchive<package>.exe` name. Both assets contain identical verified bytes,
allowing older launchers to upgrade. The original offline package 1.2.0.1 needs
one manual upgrade to gain launcher update checks. No GitHub account/token is
required, and the feed excludes draft and prerelease packages.

## Offline launch and diagnosis

From the active installation folder:

```powershell
.\Start.exe --updates     # Check now and offer installation
.\Start.exe --no-updates  # Open without a network check
```

Manual installation and repair work offline. Update-check errors are logged at
`%LOCALAPPDATA%\AstroArchive\updates\last-error.txt` by both the startup launcher
and the application's release dialog. The log retains the native Windows error
code, exact downloaded installer path, expected/saved SHA-256 and the feed's
reported signing status. When Windows blocks installer launch, it also reads
matching Code Integrity events 3033/3077 around that attempt, including their
policy IDs. Event access failures are recorded without preventing the current
app from opening or removing the verified download. Setup errors are logged at
`%TEMP%\AstroArchive-setup-error.txt`. Installer downloads for installed apps are
stored inside the installation's `updates` folder. Older launcher downloads may
remain under the per-user updates cache.

Release checks display package-specific notes, with a bounded exact-tag GitHub API
fallback for older update feeds. A notes error leaves installation available.
Downloads show byte and percentage progress; downloaded installers remain verified
before the app hands off to setup.

### Smart App Control blocks

Policy ID `{0283ac0f-fff1-49ae-ada1-8a933130cad6}` identifies Windows' usual
`VerifiedAndReputableDesktop` Smart App Control policy. The wording “Enterprise
signing level” in a Code Integrity event does not itself mean the PC is managed
by an organisation. A blocked cached installer means setup has not started;
a blocked `Start.exe` after setup identifies a separate launcher rejection.

[Microsoft's Smart App Control documentation](https://learn.microsoft.com/windows/apps/develop/smart-app-control/overview)
explains that unknown, unsigned programs are blocked when enforcement is on.
That explains why one unsigned package can work while a newly built package is
rejected. A checksum confirms the download, not publisher trust. Rebuilding,
renaming, relocating or changing how the same file is launched does not give it
a trusted signature. Follow [the signing setup](RELEASING.md#configure-windows-signing)
to sign the installer, launcher and app with a validated publisher identity.
Signing support in the workflow alone does not mean published files were signed.
