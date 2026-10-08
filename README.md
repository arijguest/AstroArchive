<div align="center">
  <img src="Application_Source/Assets/AstroArchive_Logo.png" alt="AstroArchive logo" width="220">

# AstroArchive

**Your observing sessions, organised. Your stacking inputs, ready.**

A Windows desktop app for archiving astronomical images,<br>
verifying imports and preparing files for external processing.

[![Latest release](https://img.shields.io/github/v/release/arijguest/AstroArchive?style=flat-square&color=7c6cf2)](https://github.com/arijguest/AstroArchive/releases/latest)
[![Windows build](https://github.com/arijguest/AstroArchive/actions/workflows/windows.yml/badge.svg)](https://github.com/arijguest/AstroArchive/actions/workflows/windows.yml)
![Platform: Windows 10 / 11 x64](https://img.shields.io/badge/Windows-10%20%2F%2011%20x64-168aad?style=flat-square)

**[Download for Windows](https://github.com/arijguest/AstroArchive/releases/latest)** &nbsp; · &nbsp; **[User guide](Application_Source/Quick_Start.txt)** &nbsp; · &nbsp; **[Report an issue](https://github.com/arijguest/AstroArchive/issues)**

</div>

---

## Recent changes

| Version | Highlights |
| --- | --- |
| **1.13.2** | Keep the preview frame and sky visible while loading; paint Linear pixels before background stretching, reuse decoded samples and cancel passed-over loads. |
| **1.13.1** | Faster repeat telescope scans using saved session inventories, newer folders first and an explicit full rescan. |
| **1.13.0** | Shared search, grouped import options, default PNG/JPG/JPEG exclusion, Unknown target assignment, repository purge, owned progress and Windows taskbar repair. Mosaic removed. |
| **1.12.4** | Settings, Guide and support join the left toolbar actions; a centred page selector adapts to window width and larger text. |
| **1.10.1** | Restored full-image previews, including resized and high-DPI images. A centred toolbar provides zoom, pan and a labelled **Fit** button. |
| **1.10.0** | Compact filters with exposure/gain sliders, acquisition-session labels and live result counts. Portrait preview layout, Windows policy diagnostics and optional publisher-signing support. |
| **1.9.0** | Grouped navigation menus, visible sort direction and priority, a first-run walkthrough and accessibility preferences. |
| **1.8.x** | Broader image-format support, reviewed metadata, explicit FITS conversion, dark-flat recipes, grouped target lists and an offline observing-location picker. |

[Full change history](Application_Source/README.md) · [Published releases](https://github.com/arijguest/AstroArchive/releases)

## What you can do

| Workflow | Features |
| --- | --- |
| **Import and verify** | SHA-256 verification, duplicate detection, filtered imports, failure review and recovery. Source originals stay in place by default. |
| **Manage telescopes** | Saved profiles, local USB storage detection and device renaming. Seestar/DWARF layouts plus explicit-header recognition for additional instruments. |
| **Find captures** | Browse files, targets or observing sessions. Search common names and catalogue IDs, including all 109 Caldwell objects. |
| **Review images** | Zoom, pan, display stretch and selected HDU/page/frame previews where supported. Display adjustments preserve original pixels. |
| **Prepare processing** | Verified file exports, calibration matching, dark-flat recipes and explicit derived FITS conversion for eligible images. |

Imports retain original bytes. Stacking take place in external software.

## Install on Windows

| Requirement | Details |
| --- | --- |
| **System** | Windows 10 or 11, 64-bit; .NET Framework 4.8 or later. |
| **Installer** | `AstroArchive<package-version>.exe` from the [latest release](https://github.com/arijguest/AstroArchive/releases/latest). |
| **Location** | `%LOCALAPPDATA%\Programs\AstroArchive` by default. |
| **Integration** | Desktop and Start menu shortcuts; an uninstaller in Windows Installed Apps. |

Download the installer, run it and open AstroArchive from its shortcut. Installation works offline and needs no administrator access at the default location.

<details>
<summary><strong>Checksums, signing and Windows policy blocks</strong></summary>

Each release includes SHA-256 checksums. In PowerShell, substitute your downloaded installer’s filename:

```powershell
Get-FileHash .\AstroArchive1.13.2.1.exe -Algorithm SHA256
```

Compare `Hash` with the corresponding `.sha256` file. A checksum checks file integrity; publisher identity depends on code signing.

- **Signing:** The release workflow supports trusted publisher signing when its account is configured and enabled. Check the package’s release notes for signing status.
- **Application Control:** If Windows reports that a policy blocked a file, consult [Windows signing and policy troubleshooting](docs/RELEASING.md#smartscreen-and-application-control). Managed PCs may require administrator approval of the publisher.
- **Original 1.2.0 installer:** Close the app and manually install the latest package once in the existing location to enable update checks. The [original offline installer](releases/offline-1.2.0) remains available.

</details>

## Your first import

1. **Choose an archive:** Use **Repository → Choose repository folder**, or **Settings → Repository**.
2. **Choose a source:** In **Import**, select a telescope or mirror folder and assign a unique device ID, such as `Seestar-01`.
3. **Scan and review:** Check metadata, status and the import summary. Search and filter to select the ready files you want.
4. **Import:** Copy and verify the eligible files shown. Keeping originals and skipping flagged captures are the defaults.
5. **Export:** Select repository rows with **Ctrl/Shift**, then right-click **Export** for file copies or a stacking folder with optional matching calibrations.

> **Backups and moves:** Keep `.astroarchive/index.sqlite` with the archive. Use one writer per archive, including cloud-synced folders.

## Navigation and viewing

| Control | Use |
| --- | --- |
| **Import / Export / Repository / Settings / Guide** | Top menus group workflow actions, tools, diagnostics and help. The repository path above them opens its folder. |
| **Filters** | Narrow captures by acquisition session, exposure, gain and review state. Advanced options include format, capability and mount. |
| **Columns** | Show or hide headings; drag to reorder or right-click to move left/right. Import and repository layouts save independently. |
| **Table headings** | Click to sort; **Shift-click** adds columns. Arrows and priorities show the active sort order. |
| **Preview toolbar** | Controls sit below the image. Scroll or pinch to zoom; drag the image or use arrows to move the view after zooming. **Fit** or **F** restores the whole image; **Escape** closes the popup. |
| **Capture sky** | A cached, text-free constellation globe uses only the space left after fitting the image. Hover for recorded capture time, direction and altitude. Uses capture location or your saved observing place; missing time/site shows celestial coordinates. |
| **Purple page dropdown** | Switch Repository, Edited and Import in the centre of the header without losing state. It gets a compact row when space is limited. Ctrl+1–3 selects a page; Ctrl+Tab cycles pages. |
| **Settings → Preferences** | System/light/dark themes, text size, comfortable rows, high contrast and reduced progress animation. |
| **Guide** | Repeat the first-run walkthrough, search offline help or open About. **F1** opens help for the current page. |

Target groups show file counts and known sub-exposure totals. Stacks are counted separately and excluded from those exposure totals; unknown exposure remains explicit.

## Import review and cleanup

- **Review and recovery:** Distinguish rejected captures, integrity problems and transfer failures. Review metadata evidence and conflicts before applying re-detected values; recorded user overrides retain priority.
- **Ignore failed:** Enable **Import options → Ignore failed** to skip filenames containing `failed`, regardless of case. It applies to folder, USB and Dump imports; rescan after changing it. Ignored originals remain in place.
- **Import options:** Files, Capture and Analysis tabs expose import choices beside Scan. **Ignore non-raw files (PNG/JPG/JPEG)** defaults on with a saved opt-out. Assign a target to Unknown lights/stacks before import.
- **Purge non-raw files:** Confirm removal of repository PNG/JPG/JPEG captures; Edited and scientific originals are excluded.
- **Delete failed:** The repository’s **Delete failed** tool lists matches across the active archive for confirmation, regardless of filters. Source copies and shared metadata remain; recorded checksums prevent reimport.
- **Dump inbox:** Drop FITS into the archive’s `Dump` folder before startup, or process it manually. Verified imports and duplicates are removed; failed inputs remain. Finish copying before processing.

[Complete user guide](Application_Source/Quick_Start.txt)

## Formats and processing

Import, preview and scientific export have different capabilities:

| Format | Archive / original export | Pixel operations and processing |
| --- | --- | --- |
| **FITS / gzip FITS** | Supported. | Supported image HDUs and selected slices; eligible originals can go directly into stacking projects. |
| **Tile-compressed FITS `.fz`** | Supported. | Optional CFITSIO codec for decoding and explicit derived FITS export. |
| **TIFF / PNG** | Supported. | Supported Windows pixel layouts; confirm linearity before explicit FITS conversion. |
| **JPEG** | Supported. | Display preview and original export. |
| **XISF** | Supported. | Supported numeric layouts; optional Zstandard codec. Confirm linearity before explicit FITS conversion. |
| **SER** | Supported. | Supported frame previews; export the recording for planetary processing. |
| **AVI / camera RAW** | Supported for recognised formats. | No decoder bundled; original export only. External RAW previews depend on installed Windows codecs. |

- **Derived FITS:** Conversion is explicit, records source/output checksums and leaves originals untouched. Scientific eligibility is shown separately from preview support.
- **Calibration:** Matching explains accepted, review-needed and rejected candidates. Dark flats match raw-flat exposures in a separate preparation stage.
- **Optional codecs:** CFITSIO and Zstandard are not bundled. Local codecs in `%LOCALAPPDATA%\AstroArchive\codecs` survive updates.
- **External processors:** Single-stack Siril handoffs use verified working copies. Export selected stacks for manual loading in AstroWizard.
- **Analysis:** ASTAP needs a separate installation and star database; Astrometry.net needs an account. The observing town/city picker works offline.
- **Cloud folders:** Use filesystem-mounted or streamed folders, including Google Drive for desktop. Browser-only folders cannot be used.

[Format, instrument and conversion limits](docs/COMPATIBILITY.md) · [Supported processor handoffs](docs/PROCESSOR_HANDOFFS.md)

## Updates

| Route | Behaviour |
| --- | --- |
| **Desktop / Start menu shortcut** | The launcher checks stable releases, displays package notes and offers **Install release** or **Later**. |
| **Inside AstroArchive** | Open **Settings → Check for and install new releases**, then choose **Install release**. |
| **After installation** | The app restarts; a dismissible banner confirms the installed package once. |

Downloads show progress and are verified before installation. Updates preserve archives, settings, history and local codecs; downgrades are refused. Offline or failed checks leave the installed app available.

<details>
<summary><strong>Portable copies, offline launch and troubleshooting</strong></summary>

- Running `AstroArchive.exe` directly skips the launcher’s startup check.
- Updates started from a portable copy install at the registered/default location and retain the portable copy.
- Public release checks require no GitHub login.
- See the [update guide](docs/UPDATES.md) for offline launch, repair and diagnostic log locations.

</details>

## Build from source

**C# 5 · WPF · .NET Framework 4.8 · Windows x64**

Use the .NET Framework compiler; the application build needs no Visual Studio, SDK or NuGet installation.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-release.ps1
```

- **Validation:** Application, installer, updater and Windows smoke checks. Run registration/install smoke checks on a test account.
- **Output:** `release-artifacts/`.
- **CI:** Pushes to `main` and pull requests run Windows validation; publication also checks release integrity and configured signatures.
- **Publication:** A new package version on `main` publishes only after validation. Existing published releases are retained.

| Location | Contents |
| --- | --- |
| [`Application_Source/`](Application_Source/README.md) | Application, assets, data readers and tests. |
| [`Installer/`](Installer/README.md) | Installer, launcher, Windows integration and update tests. |
| [`docs/`](docs) | Compatibility, processing, updates and release instructions. |
| [`scripts/`](scripts/build-release.ps1) | Build and release validation. |

[Release and signing guide](docs/RELEASING.md)

## Catalogue and licensing

The bundled catalogue derives from **OpenNGC by Mattia Verga and contributors**, licensed **CC BY-SA 4.0**.

[Catalogue attribution](Application_Source/Catalogue_Notice.md) · [OpenNGC provenance](Application_Source/OpenNGC_README.md)

The offline constellation figures derive from **D3-Celestial by Olaf Frohn**, under
the BSD 3-clause licence. [Constellation attribution](Application_Source/Sky_Catalogue_Notice.md).

The repository does not currently specify a licence for the application source.
