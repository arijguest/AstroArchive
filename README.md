<div align="center">
  <img src="Application_Source/Assets/AstroArchive_Logo.png" alt="AstroArchive" width="280">

# AstroArchive

A Windows desktop app for organising smart-telescope FITS captures and preparing verified stacking projects.

[![Windows build](https://github.com/arijguest/AstroArchive/actions/workflows/windows.yml/badge.svg)](https://github.com/arijguest/AstroArchive/actions/workflows/windows.yml)
[![Latest release](https://img.shields.io/github/v/release/arijguest/AstroArchive)](https://github.com/arijguest/AstroArchive/releases/latest)

**[Download the latest Windows installer](https://github.com/arijguest/AstroArchive/releases/latest)** · **[Quick start](Application_Source/Quick_Start.txt)** · **[Report an issue](https://github.com/arijguest/AstroArchive/issues)**

</div>

## Install

Windows 10 or 11, **64-bit**, with **.NET Framework 4.8 or later**.
Download `AstroArchive<package-version>.exe` from the latest
release (currently `AstroArchive1.6.1.1.exe`) and run it. Installation works offline and needs no administrator access.
The default folder is `%LOCALAPPDATA%\Programs\AstroArchive`.
Desktop and Start menu shortcuts and an Installed Apps uninstaller are included.

The installer is currently unsigned. SHA-256 checksum files accompany releases.
To check a download in PowerShell, run `Get-FileHash .\AstroArchive<package-version>.exe -Algorithm SHA256` and compare it with the release checksum.

Already using the original offline 1.2.0 installer? Close AstroArchive and install
the latest release **once** in the existing location to enable future update checks.
The [original offline installer](releases/offline-1.2.0) remains available.

## Updates

Launch AstroArchive through its Desktop or Start menu shortcut. The launcher
checks the latest stable GitHub release at startup. When an update is available,
it asks **“Install it now?” with Yes selected by default**. No opens the current
version. Yes downloads and verifies the installer, updates the existing
installation and restarts AstroArchive. Application and installer revision
versions are compared numerically; downgrades are refused.

Updates preserve repositories, images, manifests, caches and user settings.
Offline or failed checks leave the installed app available. Downloads use HTTPS
and SHA-256 verification; release checks need no GitHub login for this public
repository. Running the portable app executable directly bypasses the launcher.
Use **Settings → Check for and install new releases**, then **Install release**.
The app verifies the download, saves state, closes, installs and restarts automatically.
Portable copies install at the registered/default location.
See [update details](docs/UPDATES.md) for manual checks, troubleshooting and offline launch.

## What it does

- Detect local USB telescope storage and auto-upload missing FITS captures with verified duplicate screening.
- Save/select telescope profiles, recover them from archive records, and rename devices across the selected archive.
- Import `.fit`, `.fits`, `.fts` and gzip-compressed FITS with SHA-256 verification and duplicate detection.
- Organise captures by target, device, session and camera, including Seestar and DWARF layouts.
- Filter library and import views from a dropdown, screen failed/rejected telescope captures and import only the ready files shown.
- Track imports and deletion history with SQLite so later telescope imports skip captures you removed.
- Right-click selected files to export copies, prepare ready-to-stack folders with optional matching calibrations, edit metadata or delete archive copies.
- Process FITS dropped into the archive’s `Dump` folder on startup. Verified imports and duplicates are removed; failed inputs remain.
- Send a single FITS stack to the verified AstroWizard build or Siril using a separate working copy. See [handoff support](docs/PROCESSOR_HANDOFFS.md).
- Optionally identify targets with local ASTAP or Astrometry.net and analyse field rotation.
- Read filesystem-mounted or streamed cloud folders, including Google Drive for desktop.

Originals are retained by default. Stacking runs in external software. Archive
imports use FITS; XISF, TIFF, PNG and JPEG can be previewed. Camera RAW preview
depends on installed Windows codecs. Video and tile-compressed `.fz` need conversion.
Plate solving requires a separately configured ASTAP database or Astrometry.net account.

## Start using it

1. Open AstroArchive and choose an archive folder in Settings.
2. In Import, choose your telescope folder and assign a unique physical device ID.
3. Scan, review the detected captures and import new files.
4. Search the library, select files with Ctrl/Shift, and right-click for file tools.

Keep `.astroarchive/index.sqlite` with the archive when moving or backing it up.
Use one writer per archive. Read the [complete guide](Application_Source/Quick_Start.txt)
for classification, cloud folders, optional solving and data handling.

## Development and releases

The application is C# 5 / WPF targeting .NET Framework 4.8. Build on Windows x64
with the compiler included with Windows; no Visual Studio, SDK, NuGet or Python
installation is required.

```powershell
# Application + installer tests, build and Windows smoke checks
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-release.ps1
```

Outputs go to `release-artifacts/`. [Windows CI](.github/workflows/windows.yml)
runs on pushes and pull requests. A new package version on `main` automatically
publishes a tested installer, checksum and update feed. Existing releases stay
unchanged. See [releasing](docs/RELEASING.md) for the version bump procedure.

| Location | Purpose |
| --- | --- |
| [`Application_Source/`](Application_Source/README.md) | Application, catalogue, assets and generated-data tests |
| [`Installer/`](Installer/README.md) | Installer engine, Windows integration, launcher and update tests |
| [`scripts/`](scripts/build-release.ps1) | Windows build and smoke validation |
| [`releases/offline-1.2.0/`](releases/offline-1.2.0) | Original uploaded installer and provenance |

## Catalogue attribution

The bundled catalogue derives from **OpenNGC by Mattia Verga**, licensed
**CC BY-SA 4.0**. Attribution and provenance are in
[Catalogue_Notice.md](Application_Source/Catalogue_Notice.md) and
[OpenNGC_README.md](Application_Source/OpenNGC_README.md).
The repository does not yet specify a licence for the application source.
