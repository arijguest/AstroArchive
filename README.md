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

Updates are available from **Settings → Check for and install releases**.
Installation and uninstall preserve your repositories and settings.
For repair, offline launch or Windows policy blocks, see [installation and updates](docs/UPDATES.md).

## Start using the app

1. Choose **Repository → Choose repository folder**.
2. On **Import**, choose a source folder and assign a unique telescope ID.
3. Select **Scan folder**, review the results, and correct uncertain metadata.
4. Import the eligible files shown. Originals are kept by default; copies are verified and duplicates skipped.
5. Browse **Repository**, select files, and use **Export** or the right-click menu to prepare them for processing.

Keep the whole repository, including `.astroarchive`, together when moving or backing it up.
Use one AstroArchive writer per repository, including cloud-synced repositories.

## Find and view images

- **Repository:** choose a target, search, or open **Filters**. Repeated Light subframes are grouped into collapsed session summaries showing dates, count, exposure and filters. Expand a summary to inspect files, or choose **Repository → View → Show all files**. Stacks remain separate.
- **Tables:** click a heading to sort; Shift-click adds a sorting column. Right-click a heading to choose columns. Repository, Import and Edited remember their own layouts.
- **Preview:** scroll or pinch to zoom, drag or use arrows to pan, and choose **Fit** to recenter. Stretch affects the display only. Animated GIFs and videos have **Pause / Play** below the image.
- **Preferences:** choose appearance and accessibility options from **Settings**. **F1** opens help for the current page.

## Edited images and processing

**Edited** has targets, a table and Preview, with an **All projects** view.
Add individual images or use **Import folder** to scan and review an existing collection.
Folder imports skip repository/database folders and originals already archived, including renamed identical copies.

Starless and Stars only filenames are recognised. Object, filter, sub-count and exposure details come from available metadata or explicit filename labels. GIFs inherit missing details from a uniquely matching edited image in the same folder.

Sending a stack to **Siril** creates a verified working copy in Edited.
For AstroWizard or another editor, choose **Export → Create Edited working copies**, then load the copies from the opened project folder. Save outputs there and refresh Edited to find them.

Stacking exports keep compatible input groups and calibrations separate. **Mosaics** organises panels and completed outputs. Stacking, calibration and stitching run in your processing software.

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

- [Complete offline user guide](Application_Source/Quick_Start.txt)
- [Mosaic workflow](docs/MOSAICS.md)
- [Siril and other editors](docs/PROCESSOR_HANDOFFS.md)
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
The repository does not specify a licence for the application source.
