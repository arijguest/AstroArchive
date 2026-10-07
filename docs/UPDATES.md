# Windows updates

The installed `Start.exe` reads the latest stable release's `update.json` over
HTTPS. It offers an upgrade only if its four-part package version is newer than
the installed package and its application version is not older. The startup
prompt uses Yes as the default; No continues to the installed application.

The manifest binds an exact application version, package revision, GitHub release
asset URL, byte count and SHA-256. Only this repository's installer URL is accepted;
HTTPS redirects are restricted to GitHub and its release-asset hosts. Downloads
are bounded in size and time. TLS certificate verification remains enabled.
A checksum detects a damaged or mismatched download; it is not a code-signing
identity. The installer itself verifies all embedded payload hashes.

After acceptance and verification, the new installer waits for the launcher to
exit, applies an in-place update and restarts AstroArchive. It uses the existing
versioned folders, atomic installation record and exception rollback engine.
It refuses running applications, unrelated folders and downgrades.
Repositories and user settings are outside the managed application-file cleanup.

The original offline package (1.2.0.1) cannot check for updates. Upgrade it once
with package 1.2.0.2 or later. Installation and manual repairs remain offline.

## Manual and offline use

In AstroArchive, open **Settings → Check for and download new releases**.
Check for the latest stable release, then choose **Download release** and a save
location. The installer is verified before saving as `AstroArchive<package>.exe`
(for example, `AstroArchive1.3.0.1.exe`). Close the app before running it. A failed
check or download leaves the current installation available.

The feed retains the `url` asset expected by 1.2.0 launchers and adds
`download_url` for the concise installer name. Both assets contain identical
verified bytes; new launchers and Settings use the concise download.


From the active `app-<version>-r<revision>` installation folder:

```powershell
.\Start.exe --updates     # Check now and show the result
.\Start.exe --no-updates  # Open the app without a network check
```

Normal shortcuts check at every launch. A network failure opens the existing
app; details are written to `%LOCALAPPDATA%\AstroArchive\updates\last-error.txt`.
Downloaded installers are cached below that updates folder.
Installer errors go to `%TEMP%\AstroArchive-setup-error.txt`.
After a failed installation, run the existing shortcut or use the latest
installer manually to retry or repair.

No API token is installed in the application. Releases must remain publicly
readable for unauthenticated updates. Private distribution would need a separate
authentication design. Draft and prerelease packages are excluded by GitHub's
latest stable release endpoint.
