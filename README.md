<div align="center">
  <img src="Application_Source/Assets/AstroArchive_Logo.png" alt="AstroArchive logo" width="180">

# AstroArchive

Archive astronomical images, organise sessions and prepare verified files for processing.

**[Download for Windows](https://github.com/arijguest/AstroArchive/releases/latest)** · **[Website](https://astroarchive.arijguest.com)** · **[User guide](Application_Source/Quick_Start.txt)** · **[Report an issue](https://github.com/arijguest/AstroArchive/issues)**

</div>

## Install and start

Requires **Windows 10/11 x64** and **.NET Framework 4.8**. Run the latest installer;
the default location needs no administrator access.

1. Choose **Settings → General → Choose repository**.
2. On **Import**, choose a capture folder and a unique physical telescope ID.
3. **Scan folder**, review the metadata, then import the eligible files shown.
4. Browse **Repository** and select files for export or processing.

Imports verify copies and skip duplicates. Source originals are kept by default.
Keep the entire repository, including `.astroarchive` and `Edited`, together when
moving it. Use one running AstroArchive instance per repository.

Updates appear in **Activity** and **Guide → About AstroArchive**.
Use **Settings → Updates** to install them. [Installation, repair and policy blocks](docs/UPDATES.md).

## Latest releases

- **[3.1.2 (package 3.1.2.1)](https://github.com/arijguest/AstroArchive/releases/tag/v3.1.2.1):** pause and recover imports after restarts or updates, keep Settings available during parallel telescope downloads, see arriving files and numeric ETAs, and export dark or light branded Analytics documents.
- **[3.1.1 (package 3.1.1.1)](https://github.com/arijguest/AstroArchive/releases/tag/v3.1.1.1):** import from multiple telescopes simultaneously, select more captures while live import runs, and stop each session independently in Activity. Overlapping imports reuse verified downloads and archive each capture once.
- **[3.0.1 (package 3.0.1.1)](https://github.com/arijguest/AstroArchive/releases/tag/v3.0.1.1):** automatic Seestar/DWARF network discovery, selected and live imports, plus six analytics charts with PNG, JPEG, PDF and SVG exports.
- **[2.1.15 (package 2.1.15.1)](https://github.com/arijguest/AstroArchive/releases/tag/v2.1.15.1):** remembered selections and initial previews, keyboard navigation, optional clearer selection outlines, and maintenance to fill missing metadata. Recent 2.1 releases also added video durations, target exposure totals and batch metadata editing.

See the [release history](https://github.com/arijguest/AstroArchive/releases) for full notes.

## Import recovery and Analytics documents

Import queues support Pause and Resume, including recovery after an app restart,
crash or update. Activity retains unfinished folder, USB, network/live, Dump and
edited-image imports. Resume verifies completed archive copies and restarts
incomplete files. Each telescope has independent controls, and Settings, filters,
grouping and Analytics stay available during simultaneous downloads. Arriving
network files appear in Import with transfer progress and measured ETAs.

Analytics documents default to Dark with an optional Light theme. All formats
use the branded palette, larger AstroArchive logo, and concise headers and footers.

## Network and live imports

Released versions include direct, read-only Seestar and DWARF imports. Install
the latest Windows package above. If copying a portable build, keep the complete
folder, including `SMBLibrary.dll` beside `AstroArchive.exe` and the bundled notices.

On **Import**, choose **Connect over network…** beside **Saved telescope**.
Keep the PC and telescope on the same Wi-Fi/LAN, select a discovered telescope,
and connect. Choose its saved physical telescope profile or name a new one.

- **Select files** lets you browse capture folders and tick individual files.
- **Search all** selects supported captures across the storage for review.
- **Start live import** watches the displayed folder and subfolders for new captures
  while the telescope app continues shooting. Existing captures are excluded by default.
- **Connect over network…** stays available during live import. Reconnect to select
  more files, or connect another telescope using its own saved physical telescope name.
- **Activity** shows each session's telescope, progress, **Pause** and **Stop live import** or
  **Cancel download** controls. Paused or interrupted jobs offer **Resume**.
  The **Live import underway** indicator opens Activity;
  **Stop all telescope imports** stops every network session.
- **Advanced…** provides manual IP/path access, DWARF FTP credentials, polling,
  transfer limits and an option to include existing files when live import starts.

Large selections warn that network transfers take longer than USB. Live imports
wait for stable files and retry incomplete captures or brief disconnects. Originals
stay on the telescope; local staging needs space as well as the final archive.
Stopping sessions keeps completed archive copies and verified downloads. Closing
the app pauses unfinished queues and waits for transfers to stop;
repository switching and conflicting archive operations wait until all network sessions finish. Network sessions in the same
AstroArchive instance coordinate their archive writes.
File access depends on telescope firmware. Seestar access uses its own SMB client
and does not require changing Windows guest/signing policies.

See [setup, live imports and troubleshooting](docs/REMOTE_IMPORT.md).

## Browse and preview

- **Targets:** preferred ID before the name, such as **M31 - Andromeda Galaxy**. Planet names in filenames are recognised during import. Planets and comets share the Solar system group; Meteors sit above Other targets. **All Targets** is bold and shaded.
- **Search:** combine object, device and type, such as **M45 Dwarflab**, **M45 S50 Pro** or **M45 stack**. Use quotes or **file:** for literal filename text.
- **Sessions and videos:** condensed sub rows show dates, count, exposure per sub and total integration. Expand a row or switch off **Repository → Group subs by session**. Stacks with known counts show **Stack (1445)**; the filter remains **Stack**. Recordings use **Video**, show duration in Exposure and appear above grouped subs. Target totals include known video durations; missing durations remain unknown.
- **Selection:** keep files selected while moving between targets. A plain file-row click replaces the batch; Ctrl-click toggles and Shift-click selects a range. Right-clicking a selected entry preserves the batch. The target-pane counter shows the total; click it or press Escape to clear it.
- **Tables:** click headings to sort; Shift-click adds a sort. Right-click to choose columns. Columns adapt to window and preview-pane resizing; each page saves its own layout. Left/Right moves through columns, switching panes at the table edges; Up/Down selects within the active pane.
- **Preview:** Repository and Edited load the first All Targets entry on first opening and remember later selections for the session. Sampled sidebars keep browsing light; large still/SER popups decode native pixels within format limits. Zoom, pan, Fit and stretch affect display only. GIF, SER and supported videos offer Pause/Play.

The page selector switches **Repository**, **Edited** and **Import**.
**Ctrl+1–3** switches pages; **F1** opens help. Less obvious menu actions have short
tooltips. Checkboxes distinguish off, on, mixed and unavailable states.
**Settings → Accessibility → Clearer selections** adds prominent selection outlines;
it is off by default.

## Analytics and reports

**Repository → Analytics** previews six branded charts: targets photographed,
imaging timeline, time per target, time per telescope, filter mix and exposure
lengths. Documents default to **Dark**; choose **Light** for a light background.
Scope by telescope and acquisition dates, add a document label and optionally
include rejected light frames. Scope starts with the entire repository independently
of browsing filters and selection. Export **PNG, JPEG, PDF or SVG**, with 150 or
300 DPI for raster images. **Export all** saves one document; PDF uses landscape pages and image/SVG
exports use a combined sheet. Long rankings include every target and telescope on
continuation pages. Time means individual light-frame integration; stacks, videos
and calibrations are excluded. Unknown exposures and dates are reported.
Exports leave the archive and capture files unchanged.

## Edited files and export

**Edited** keeps working copies and returned editor outputs with the archive.

- Add images or scan a folder. Identical images are skipped; changed files can become new versions.
- Edit targets, filters, class, exposures or coordinates without rewriting image files.
- RAW/scientific types are grouped first by file type; GIFs appear last.
- Right-click **Export files…** to copy one or more selected images with byte verification.
- Right-click **Delete files…**, or press Delete, to confirm removal of Edited copies. Source originals and archived captures remain.

**Export to…** hands supported inputs to PixInsight, Siril, DSS, GIMP, Photoshop,
AS!4, AstroWizard or Stacking Wizard. **Settings → Export** stores application
defaults and locations. Save outputs alongside a working copy and refresh Edited.

**Export files…** copies originals; **Stacking folder…** groups compatible inputs
and optional calibrations. Existing files are retained; collisions receive numbered
suffixes. **Add Metadata** and **Create new folder** default off. Stacking and
calibration run in your processing software. [Handoff details](docs/PROCESSOR_HANDOFFS.md).

## Formats and data safety

| Files | Support |
| --- | --- |
| FITS / gzip FITS / XISF / TIFF / PNG | Supported scientific previews; explicit FITS conversion for eligible linear inputs. |
| JPEG / GIF | Still or animated display; original-file export. |
| SER | Planetary playback and frame selection; native recording export. |
| AVI / MP4 / MOV / M4V / WMV / MKV | Playback with installed Windows codecs; original-file export. |
| CR2 / CR3 / NEF / ARW / DNG | Original-file import/export; previews need a compatible Windows codec. |
| Tile-compressed FITS / Zstandard XISF | Optional CFITSIO / Zstandard codecs for decoding. |

Imports and ordinary exports preserve original bytes. Missing or conflicting
metadata is not guessed. [Format and decoder limits](docs/COMPATIBILITY.md).

**Edit metadata** applies to the selected batch across targets or Edited projects;
expanding changes to whole capture sessions is optional. **Repository → Maintenance →
Fill missing metadata** previews supported additions from original headers, filenames
and preserved session metadata. Existing values, user edits and original files stay
intact; conflicting or unsupported fields remain unknown. Unknown camera channels
default to Tele for DSO and solar-system targets when the evidence allows it.

**Repository → Back up archive…** creates a verified folder or lossless ZIP,
including images, Edited and the database. Choose a separate drive when possible.
Restore by selecting the backup's Repository folder.

**Settings → Backups → Protect originals** can block accidental deletion/renaming
on local NTFS drives. Edited remains writable. Protection is optional and is not a
backup. Archive deletion keeps source originals; deletion history controls reimport.

## Documentation

- [Offline user guide](Application_Source/Quick_Start.txt)
- [Repeat telescope imports](docs/FAST_IMPORTS.md)
- [Network and live telescope imports](docs/REMOTE_IMPORT.md)
- [Processing handoffs](docs/PROCESSOR_HANDOFFS.md)
- [Installation and updates](docs/UPDATES.md)
- [File and metadata safety](docs/SECURITY.md)
- [Build and focused checks](docs/DEVELOPMENT.md)
- [Release and signing](docs/RELEASING.md)

## Licence and credits

Source-available under [PolyForm Noncommercial 1.0.0](LICENSE).
Commercial use requires separate permission from Ari J. Guest; your images remain yours.
See [licensing and required notices](LICENSING.md).

Catalogue data: [OpenNGC](Application_Source/Catalogue_Notice.md), CC BY-SA 4.0;
[GeoNames](Application_Source/City_Catalogue_Notice.md), CC BY 4.0;
[D3-Celestial constellation figures](Application_Source/Sky_Catalogue_Notice.md), BSD 3-clause.
Network access: [SMBLibrary](Application_Source/Remote/lib/README.md), LGPL-3.0-or-later,
bundled as a separate, replaceable DLL with matching source and licence notices.
