<div align="center">
  <img src="Application_Source/Assets/AstroArchive_Logo.png" alt="AstroArchive logo" width="180">

# AstroArchive

Archive astronomical images, organise observing sessions, and prepare files for processing.

**[Download for Windows](https://github.com/arijguest/AstroArchive/releases/latest)** · **[User guide](Application_Source/Quick_Start.txt)** · **[Report an issue](https://github.com/arijguest/AstroArchive/issues)**

</div>

## Install

Requires **Windows 10 or 11, 64-bit**, with **.NET Framework 4.8**.
Download the installer from the latest release and run it. The default location is
`%LOCALAPPDATA%\Programs\AstroArchive`; no administrator access is needed there.
Open AstroArchive from its desktop or Start menu shortcut.

Updates are available from **Settings → Updates → Check for updates**.
Installation and uninstall preserve your repositories and settings.
For repair, offline launch or Windows policy blocks, see [installation and updates](docs/UPDATES.md).

## Start using the app

1. Choose **Settings → General → Choose repository**.
2. On **Import**, choose a source folder and assign a unique telescope ID.
3. Select **Scan folder**, review the results, and correct uncertain metadata.
4. Import the eligible files shown. Originals are kept by default; copies are verified and duplicates skipped.
5. Browse **Repository**, select files, and use **Export** or the right-click menu to prepare them for processing.

Keep the whole repository, including `.astroarchive`, together when moving or backing it up.
Use one AstroArchive writer per repository, including cloud-synced repositories.

## Find and view images

- **Repository:** choose a target, search, or open **Filters**. Repeated Light subframes are grouped into collapsed session summaries showing dates, count, exposure and filters. Expand a summary to inspect files, or turn off **Repository → Group subs by session**. Stacks remain separate.
- **Tables:** click a heading to sort; Shift-click adds a sorting column. Right-click a heading to choose columns. Repository, Import and Edited remember their own layouts.
- **Preview:** scroll or pinch to zoom, drag or use arrows to pan, and choose **Fit** to recenter. Large previews open in source orientation and offer rotate buttons. Stretch affects the display only. Animated GIFs and videos have **Pause / Play** below the image.
- **Navigation:** the purple page selector switches Repository, Edited and Import. Ctrl+1–3 selects a page; Ctrl+Tab cycles pages.
- **Preferences:** choose appearance and accessibility options from **Settings**. **F1** opens help for the current page.

## Edited images and processing

**Edited** shows all imports and working copies together, with targets, a table and Preview.
Add individual images or use **Import folder** to scan and review an existing collection.
Folder imports skip repository/database folders and originals already archived, including renamed identical copies. Identical files already in Edited are skipped; changed files with the same name can be added as new versions. Unchecked entries leave existing images untouched.

Select images and right-click **Edit metadata** to assign targets, filters, image class, exposures, sub counts or coordinates. Assignments persist with the repository and do not rewrite image files.

Starless and Stars only filenames are recognised. Object, filter, sub-count and exposure details come from available metadata or explicit filename labels. GIFs inherit missing details from a uniquely matching edited image in the same folder.

Choose **Export → Export to…** for PixInsight, Siril, DSS, GIMP, Photoshop, AS!4,
AstroWizard or Stacking Wizard. Supported images open from verified working copies
in Edited; subframes are exported to an input folder. DSS loads prepared file lists;
some stackers require you to choose the exported inputs in their own window.
**Settings → Export** stores defaults by file type and optional app
locations. AstroArchive tries automatic detection, then asks for the executable when
needed. Save outputs alongside the Edited working copy and refresh Edited to find them.

The compact Export menu also offers **Export files…**, **Stacking folder…** and
**Catalogue CSV**. Right-click files to create Edited copies. Optional metadata and
advanced stacking controls live under More options.

Exports default to image files only: stacks copy directly to the destination; subs
retain compatible input folders. **Add Metadata** and **Create new folder** are off
by default. Existing files are retained, with numbered suffixes for collisions.
Stacking exports keep compatible input groups and calibrations separate. Stacking and calibration run in your processing software.

## Archive safety and backups

Settings > Backups keeps backup and protection controls visible. Repository > Back up archive… opens folder and lossless ZIP choices directly. Protect originals offers optional protection against deleting or renaming archived originals in File Explorer on local NTFS drives. Edited files, Dump and archive metadata remain writable. Protection has no background scan and can be turned off from the same section. The Windows owner can still deliberately change permissions.

Back up archive… creates a verified folder or lossless ZIP outside the archive, including images, Edited files and the current database. ZIP compression preserves image resolution and every original byte. Creation and verification can take a long time; cancellation keeps earlier backups. To restore, extract the ZIP if needed and choose its `Repository` folder in AstroArchive. Restoration instructions and SHA-256 checksums are included. A separate drive protects against archive drive failure.

## Formats

| Files | Preview and processing |
| --- | --- |
| FITS / gzip FITS | Image HDUs and selected cube slices; eligible linear inputs can be exported for stacking. |
| TIFF / PNG / XISF | Supported pixel layouts; explicit FITS conversion for eligible linear images. |
| JPEG / GIF | Still or animated display; original-file export. |
| SER | Planetary recording playback and frame selection; export to planetary software. |
| AVI / MP4 / MOV / M4V / WMV / MKV | Playback using codecs installed in Windows; original-file export. |
| CR2 / CR3 / NEF / ARW / DNG | Original-file import/export; external preview needs a compatible Windows codec. |
| Tile-compressed FITS / Zstandard XISF | Optional CFITSIO / Zstandard codecs for pixel decoding. |

Imports and ordinary exports preserve original bytes. Unknown acquisition values remain unknown.
See [format and metadata details](docs/COMPATIBILITY.md).

## Guides

- [Fast repeat telescope imports](docs/FAST_IMPORTS.md)
- [Complete offline user guide](Application_Source/Quick_Start.txt)
- [Processor handoffs, formats and application detection](docs/PROCESSOR_HANDOFFS.md)
- [Installation, updates and troubleshooting](docs/UPDATES.md)

## Build from source

The app uses C# 5, WPF and .NET Framework 4.8 on Windows x64.
See [build and test instructions](docs/DEVELOPMENT.md) and [release/signing instructions](docs/RELEASING.md).

## Credits

OpenNGC by Mattia Verga and contributors supplies the object catalogue under
[CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/).
GeoNames supplies the offline place catalogue under
[CC BY 4.0](https://creativecommons.org/licenses/by/4.0/).
See the [object catalogue notice](Application_Source/Catalogue_Notice.md) and
[place catalogue notice](Application_Source/City_Catalogue_Notice.md).
The constellation figures derive from D3-Celestial by Olaf Frohn under the BSD
3-clause licence; see the [sky catalogue notice](Application_Source/Sky_Catalogue_Notice.md).

AstroArchive is source-available under [PolyForm Noncommercial 1.0.0](LICENSE).
Noncommercial use, modification and sharing are permitted under its terms.
Commercial use requires separate permission from Ari J. Guest. Your images and
outputs remain yours; third-party data retains its own licences.
See [licensing and required notices](LICENSING.md).
