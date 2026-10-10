# AstroArchive for Linux — 3.1.4 preview

This is the Linux x64 desktop release based on AstroArchive 3.1.4. It includes its
.NET 10 runtime. Supported targets are Ubuntu 22.04/24.04 x64 and Debian 12/13 x64
with a graphical desktop. Wayland desktops currently use XWayland.

Install the Debian package with `sudo apt install ./astroarchive_3.1.4-preview.1_amd64.deb`,
or extract the tarball and run `./AstroArchive`. Check `SHA256SUMS` before installing.
The tarball also needs the distribution's ICU, OpenSSL 3, fontconfig and X11 libraries.
Zstandard and compressed FITS previews use optional distribution libzstd/libcfitsio
packages. Unavailable codecs report their limitation; original files remain exportable.

## Shared drives and Windows compatibility

Open the same archive folder on either platform. Capture files, the SQLite schema,
Microsoft JSON dates, capture paths, sidecars and Edited project format stay compatible
with Windows 3.1.1–3.1.4. Linux translates Windows separators in memory and writes the
existing Windows separator convention to disk. Opening on Linux rotates the existing
archive cache identity before writes so Windows reloads the drive's latest index.
Keep the entire archive folder, including `.astroarchive`, on the drive.

Close AstroArchive before unplugging the drive, then eject/unmount it normally.
Use exFAT for a drive that both operating systems must write without extra drivers.
NTFS also works with a writable Linux NTFS mount; Windows NTFS permissions can affect
write access. FAT32 cannot hold a file larger than 4 GiB. ext4 requires a Windows driver.
Use one writer at a time. A writable local mount is required; network and cloud-backed
archives are outside this preview's tested support.

Windows deletion protection is a Windows feature. Its settings are preserved on Linux;
Linux does not enforce its NTFS rules. Protected archives permit browsing, exports,
working copies and backups; disable protection on Windows before importing or moving
captures on Linux. Complete any interrupted protection change on
Windows before opening the drive on Linux. Backups omit Windows protection settings.

## Available workflows

- Repository: shared search and categorical/exposure filters, scientific and raster
  previews, sky globe navigation, SHA-256 verification, all shared metadata fields,
  screening, reviewed target solving, rotation analysis, deletion audit/reimport,
  configurable original/stacking exports and processing-app handoff.
- Import: folders, mounted cards/shares, Dump inbox, preferences, saved telescope
  profiles, rename/recovery, optional analysis, native DWARF/Seestar discovery,
  Direct SMB/FTP transfers, live import, and persisted pause/restart recovery.
- Edited: working copies, selected or recursive finished-image imports, metadata
  overrides, preview/details, verified export, editor handoff and explicit deletion.
- Analytics: six scoped reports; six branded layouts with light/dark themes; complete SVG/JSON/PDF/PNG/JPEG exports and animated GIF/MP4 stories.
- Settings/Guide: appearance, text scale, observing site, ASTAP/Astrometry.net,
  keyring credentials, Linux capture guard and verified folder/ZIP backups.

ASTAP requires an installed Linux executable and star catalogue. Astrometry.net
requires an API key; choose online solving explicitly. Only detected star coordinates
and image dimensions are uploaded. Configure solvers/site in Settings before enabling
optional automatic analysis. Automatic analysis starts off and currently follows local
imports; network captures can be analyzed from Repository after stopping the import.

Install `libsecret-tools` and unlock your desktop Secret Service keyring to save
credentials. Without a keyring, passwords/API keys work for the current session and
must be entered again after restart. No plaintext credential fallback is written.
Linux executable paths, keyring references and import recovery records are machine
settings; they do not travel with a shared archive. Settings live in
`$XDG_CONFIG_HOME/astroarchive/settings.json`, or `~/.config/astroarchive/settings.json`.

Numeric PNG/TIFF samples retain their precision where supported. Other raster layouts
are marked display-only; scientific conversion requires supported numeric data and
confirmed acquisition linearity. JPEG/GIF still previews and the system viewer are
available; install `xdg-utils` and a default viewer for movie/animation playback.

Source and Dump originals are retained. Cancel pauses safely; Import can resume a
saved job after restart. Linux capture protection blocks deletion/relocation within
AstroArchive; filesystem tools and Windows do not enforce this Linux application guard.
Backups omit protection settings. Windows NTFS protection remains separate.

Install `ffmpeg` for H.264 MP4 stories. GIF uses the bundled managed encoder.

Install the next Linux package manually or replace the extracted application folder.
There is no Linux executable updater. See [FEATURE-PARITY.md](FEATURE-PARITY.md) for
workflow coverage, remaining presentation/platform differences and hardware limits.

## Reproduce validation

Install .NET SDK 10.0.401 and the desktop libraries, then from the repository root:

```sh
dotnet run --project CrossPlatform/EngineTests -- /tmp/astroarchive-engine-unique
dotnet test CrossPlatform/Desktop.Tests
bash scripts/build-linux.sh
tar -xzf linux-artifacts/AstroArchive-3.1.4-preview.1-linux-x64.tar.gz -C /tmp
xvfb-run -a /tmp/AstroArchive-3.1.4-preview.1-linux-x64/AstroArchive --ui-smoke /tmp/astroarchive-ui-unique
```

`AstroArchive --self-test /tmp/unique-directory` tests the installed executable's
imports, exports, working copies, backups and reopened index without needing an SDK.
`--ui-smoke` drives all six actual pages and advanced workflows, saving 21 PNG screenshots; it needs a display.
Use a new empty test directory for each invocation. GitHub CI exchanges real fixtures
between Linux and the Windows Framework engine in both directions.

Required Notice: Copyright 2026 Ari J. Guest (https://arijguest.com)

AstroArchive is distributed under PolyForm Noncommercial 1.0.0. See LICENSE and
THIRD-PARTY-NOTICES.txt in the application package.
