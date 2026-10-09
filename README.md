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
moving it. Use one writer per repository.

Updates appear in **Activity** and **Guide → About AstroArchive**.
Use **Settings → Updates** to install them. [Installation, repair and policy blocks](docs/UPDATES.md).

## Browse and preview

- **Targets:** preferred ID before the name, such as **M31 - Andromeda Galaxy**. Planet names in filenames are recognised during import. Planets and comets share the Solar system group; Meteors sit above Other targets. **All Targets** is bold and shaded.
- **Search:** combine object, device and type, such as **M45 Dwarflab**, **M45 S50 Pro** or **M45 stack**. Use quotes or **file:** for literal filename text.
- **Sessions:** condensed sub rows show dates, count, exposure per sub and total integration. Expand a row or switch off **Repository → Group subs by session**. Stacks with known counts show **Stack (1445)**; the filter remains **Stack**.
- **Selection:** keep files selected while moving between targets. The target-pane counter shows the total; click it or press Escape to clear the batch.
- **Tables:** click headings to sort; Shift-click adds a sort. Right-click to choose columns. Each page saves its own layout.
- **Preview:** sampled sidebars keep browsing light; large still/SER popups decode native pixels within format limits. Zoom, pan, Fit and stretch affect display only. GIF, SER and supported videos offer Pause/Play.

The page selector switches **Repository**, **Edited** and **Import**.
**Ctrl+1–3** switches pages; **F1** opens help. Less obvious menu actions have short
tooltips. Checkboxes distinguish off, on, mixed and unavailable states.

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

**Repository → Back up archive…** creates a verified folder or lossless ZIP,
including images, Edited and the database. Choose a separate drive when possible.
Restore by selecting the backup's Repository folder.

**Settings → Backups → Protect originals** can block accidental deletion/renaming
on local NTFS drives. Edited remains writable. Protection is optional and is not a
backup. Archive deletion keeps source originals; deletion history controls reimport.

## Documentation

- [Offline user guide](Application_Source/Quick_Start.txt)
- [Repeat telescope imports](docs/FAST_IMPORTS.md)
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
