# AstroArchive 1.11.0

Edited projects support reviewed folder import, starless/stars-only classification, acquisition metadata recovery and automatic editor working copies. The repository path sits below the table; operation feedback appears only while busy.

Portable Windows 10/11 x64 WPF app for archiving astronomical image originals and creating verified stacking projects. Open `AstroArchive.exe`; read `Quick_Start.txt` for the complete workflow.

## Changes in 1.10.1

- Fix WPF clipping the bitmap before the portrait fit transform, restoring the whole image in sidebar and popup previews, including resized and high-DPI bitmaps.
- Use a centred, single-row preview toolbar with a labelled Fit button. Pan and zoom-out controls enable after zooming; overlay controls retain wheel, pinch, drag and keyboard support.
- Windows rendering checks inspect all four image quadrants in 16 fit/resize/DPI cases and exercise zoom, pan and Fit button actions.

## Changes in 1.10.0

- Compact acquisition-session filters with exposure/gain range sliders, combined review choices and retained mosaic/format/mount options.
- Portrait sidebar and popup previews with overlay controls and scroll/pinch/keyboard input. Source pixels are unchanged.
- Windows policy diagnostics and optional publisher-signing integration.

## Changes in 1.9.0

- Grouped top-left navigation and a clickable repository path, with essential page actions and a quieter status bar.
- Visible table sort direction/priority, repeatable first-run walkthrough and an About page for Ari J. Guest.
- Accessibility preferences for larger text, comfortable rows, high contrast and reduced motion.
- Windows UI smoke tests render both themes and exercise menus, sorting, walkthrough and preferences.

## Changes in 1.8.0

- Verified FITS/XISF/raster/SER importing, selected images and explicit derived FITS exports.
- Versioned instrument/software profiles, evidence and field-level metadata review.
- Conservative calibration matching and exposure-specific dark-flat recipes.
- See [compatibility guide](../docs/COMPATIBILITY.md) for pixel layouts, optional codecs and validation limits.

## Changes in 1.7.0

- Metadata-first mosaic collections, stable panel assignments, explicit review states and completed-output roles across targets and sessions.
- Existing-WCS footprints and conservative pointing suggestions, with optional cached representative solving.
- Portable collection manifests and panel-aware stacking exports that preserve capture/calibration separation.
- See [mosaic guide](../docs/MOSAICS.md) for supported metadata, recovery, uncertainty and validation limits.

## Changes in 1.6.1

- Auto/Strong previews compensate for IRCUT/LP filters and severe colour casts.
- Solar, lunar and planetary previews display linearly; Sun/Solar labels merge into Sun without moving existing archive files.
- Larger installer and update dialogues with wrapping text and a scrollable setup body.

- Startup Dump inbox imports and verified duplicate cleanup, with retry controls in Settings.
- Direct single-stack handoff to the verified AstroWizard build and Siril. [Supported handoffs](../docs/PROCESSOR_HANDOFFS.md).

## Changes in 1.6.0

- Pinch/wheel preview zoom and pan using the colour decoders; persistent numeric/Shift-column sorting, explicit bias rows, and dashes for unknown metadata except target names.
- Install release saves settings/index, verifies setup inside the application files and installs/restarts automatically. Package revisions are included in portable update comparisons.
- Local USB telescope detection and auto-upload, with saved profiles that survive changed drive letters and can be recovered from archive metadata.
- Telescope renaming updates saved profiles, archive paths, index/manifests and deletion history while preserving capture bytes and session identity. Interrupted renames recover on reopen.
- One Filters menu for both library and import results, including camera, night, session, calibration, exposure, dimensions, status and review. Import copies the ready files in the filtered view and shows their count.
- Compact import setup with collapsed options, failure screening, durable checksum-based deletion history, and USB imports that leave flagged captures for review.
- Export, file context menus and repository tools no longer draw the native icon gutter over menu text.
- Menus, submenus and dropdowns share a popup layout that reserves separate space for scrollbars and hides them when all items fit.
- Scrollbars and menu separators follow the selected theme throughout the application. Long popups remain scrollable within the screen.

## Changes in 1.5.0

- Immediate elapsed/progress feedback and rolling ETA through copy and destination verification, shared by imports and project/selected-file exports.
- Discovery spills pending paths locally instead of waiting for metadata; bounded metadata workers share one session JSON cache.
- Indexed filename phrases, inexpensive isolated frame snapshots, validated pre-import header caching and explicit classification/header timings.
- Reused transfer buffers, indexed duplicate lookups, smaller comparable worker trials and unchanged verified checkpoint reuse.
- Provider stalls and finalisation have explicit progress states; local filesystem completion does not certify a later provider upload.

## Changes in 1.4.0

- Compact six-column library, smaller header, resizable preview pane, and settings categories.
- Persistent System/Light/Dark appearance with a quick theme switch; dialogs and menus share the theme.
- Cancellable colour previews for FITS/gzip FITS and XISF, with Linear, Auto, Strong and per-channel stretch. CFA metadata is respected; previews never modify source pixels.
- TIFF, PNG, JPEG, BMP, GIF and Windows photo codecs, preserving high-depth samples. Camera RAW formats need a compatible installed Windows codec.
- Preview/path-copy actions and selection-only CSV exports alongside the upstream file tools.

## Guide and tooltips

Hover over controls and table headers for explanations. **Guide** offers common
help topics, full-text search, troubleshooting, shortcuts and a frame glossary.
**F1** opens help for the current tab; **Ctrl+F** searches within the help window.
The guide is bundled for offline use and can be saved as text. The import tab is
labelled **Import**.

## Changes in 1.3.0

- Right-click selected library rows to export copies, create a ready-to-stack folder, edit metadata, locate files or delete selected archive copies after confirmation.
- Stacking exports offer optional matching calibrations, report availability and keep multiple targets separate. Rejected calibration candidates are omitted; unknown calibration status remains opt-in.
- Repository selection lives in Settings. A compact path stays above the library.
- Settings checks the latest stable release and installs/restarts after saving state and verifying the downloaded installer.

## Changes in 1.2.0

- Single-pass streamed discovery through a 128-file bounded queue, cached enumeration attributes and shared session JSON. Live Import/Repository rows, stage performance, files/s, processed MB/s and ETA.
- Indexed SQLite source manifest with path, file identity, size, modification/change times, SHA-256, destination, metadata and completion status. Stable verified NTFS/ReFS files can skip content reads; cloud/unsupported metadata never establishes a duplicate.
- Source hashing during temporary copying, destination verification, coordinated transactions and guarded source removal. Limited retries isolate per-file failures. Disk-full errors stop safely with committed copies retained.
- A local working SQLite index with FULL synchronous rollback-journal transactions and verified portable snapshots avoids an active database on a provider-backed archive. Snapshot backups use SQLite's backup API and self-contained rollback-journal destinations. Error reports include paths and native/SQLite codes.
- Adaptive 1/2/4/8-worker trials use real import batches, starting at 1 and retaining a faster result. Fixed worker counts remain available in Settings. Workload and cache effects apply.
- Solving and rotation are off by default. Import selects Off, Ambiguous only or All, warns about runtime, and runs analysis after copying. Representative/session analysis and content-validated caches avoid unnecessary repetition.
- Automatically detected cloud placeholders separate provider read/download waiting from local copying. Nonredirecting cloud placeholder directories are allowed; directory symlinks/junctions are skipped. Offline inputs avoid on-demand download delays.
- DWARF `cam_0`/camera 0 = telephoto, `cam_1`/camera 1 = wide. Conflicts remain unknown. Camera-specific calibration matching and export groups never mix channels.
- Stacking projects have an off-by-default Separate sessions option. Compatible sessions merge by default; opting in gives each session its own subdirectory. Merged calibration must match every light.
- The Clear filter button stays. Settings adds a red archive reset requiring a completed slider drag and a separate permanent-delete click. Sources and unindexed files remain outside its scope.

Previous improvements remain: native Windows Explorer folder dialogs with mounted/cloud navigation, per-file telescope make detection, opt-in verified original cleanup, filename-first target recognition, cyan/violet logo and a navy WPF interface with visible keyboard focus.

## Build and test on Windows

The built-in .NET Framework 4.8 compiler is sufficient. No NuGet, Python or SDK downloads are required.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\test.ps1
.\dist\AstroArchive.exe --ui-test .\ui-preview
```

`dist/AstroArchive.exe` embeds XAML, catalogue, logo, icons and Windows manifest. Settings/API-key protection and the working database use the Windows user's local app data. `.astroarchive/index.sqlite` is the portable snapshot and must stay with the archive. Run one writer for a given archive; cloud services do not provide distributed SQLite coordination. The code does not authenticate to Google Drive or require a particular mounted drive letter.

The UI smoke option creates synthetic rows, checks filtering/defaults, renders light/dark/hidden-preview layouts, and exercises PNG/JPEG/high-depth TIFF codecs without importing user files. The console suite creates isolated generated-FITS data and an ASTAP protocol double. See `Validation.txt` for the packaged build's results. The Windows release workflow runs native file identity/deletion, WPF and installer checks. Physical telescope USB behaviour, live Google Drive and authenticated plate solving require separate hardware/provider validation. The protocol double does not solve real astronomical images.

## Performance and failure handling

Stage seconds are active wall time with overlapping worker scopes merged per stage; stage times are not additive. Byte counters describe consumed app data, not physical disk or network traffic. Gzip header/pixel parsing counts decoded bytes. External ASTAP/star-database reads are not observable. Cloud availability includes provider opens/reads and can include cached reads, not solely downloads. ETA applies to the active phase and becomes available after measurable progress.

Manifest shortcuts require verified content plus unchanged trusted identity/change metadata on both source and archive copy. Name, size and mtime alone are insufficient. New source files are fully hashed during copying. Destination bytes are read back before the record is committed. Original deletion adds durable indexing, final stamp checks and locked-handle physical-boundary/hash checks. Originals remain on failed cleanup, and duplicate originals are retained.

The previous user's time-dependent import failure could not be reproduced against their actual PC or diagnosed without its error. The code remedies batch-wide aborts, transient lock handling, cloud hydration state checks and provider-backed active SQLite. It logs remaining failures for diagnosis instead of silently reporting success.

## File and classification scope

See [image and telescope compatibility](../docs/COMPATIBILITY.md) for import formats, science eligibility, optional codecs, container selection, metadata review and calibration recipes.

Preview supports FITS RGB and Bayer CFA, and XISF mono/RGB/CFA images with integer or floating samples, planar or interleaved storage, attached or base64 data, zlib/LZ4/LZ4HC compression, and byte shuffle. External XISF previews retain their 256 MB limit; indexed-image decoding supports optional Zstandard within its 32-million-sample limit. Complex samples remain original-file archives. Standard raster and camera RAW previews use installed Windows codecs. Large previews are sampled to about 1400 pixels per side; zoom scales the display sample. Release 1.8.0 extends verified import to supported raster/XISF/SER files, with explicit derived FITS export and separate capabilities. Existing external preview limits remain in effect.

Metadata corrections may move archived copies without modifying FITS pixels/headers. Exports create new folders and do not overwrite projects. Stacking itself is performed in external software.

## Source layout

- `Model.cs`, `FileState.cs`: metadata, settings, file identity/change stamps and cloud/reparse handling.
- `Fits.cs`, `Classification.cs`, `InstrumentDetection.cs`, `CameraDetection.cs`: parsing, catalogue, filename/header/structure classification and session metadata cache.
- `Repository.cs`, `ImportEngine.cs`, `FileTransfer.cs`, `Pipeline.cs`: SQLite index/manifest, bounded discovery/copy pipeline, hashing, retries, worker tuning and telemetry.
- `SourceCleanup.cs`, `ArchiveReset.cs`, `FileDeletion.cs`, `DeletionHistory.cs`: verified source cleanup, scoped archive deletion and portable exclusions.
- `CaptureScreening.cs`, `Filters.cs`, `FiltersUi.cs`: telescope failure screening and shared library/import filters.
- `Rotation.cs`, `PlateSolve.cs`: star matching, mount inference, ASTAP/Astrometry.net and candidate matching.
- `Export.cs`: session-aware verified projects and calibration safeguards.
- `PreviewData.cs`, `Xisf.cs`, `PreviewUi.cs`, `Theme.cs`: bounded image samples, stretch, native XISF decoding, preview pane and appearance.
- `App.cs`, `FileToolsUi.cs`, `ReleasesUi.cs`, `MainWindow.xaml`, `NativeFolderPicker.cs`, `Assets/`: WPF interface, native selectors and embedded branding.
- `Tests.cs`, `PreviewTests.cs`, `MockAstap.cs.txt`, `test.ps1`, `build.ps1`: generated-data checks and Windows build.

## External documentation and catalogue

- ASTAP: https://www.hnsky.org/astap.htm
- Astrometry.net API: https://astrometry.net/doc/net/api.html
- DWARF file/camera layout: https://help.dwarflab.com/en/docs/How-to-View-and-Obtain-the-Files-on-DWARF-3
- Google Drive streaming/offline modes: https://support.google.com/drive/answer/13401938
- SQLite backup API: https://www.sqlite.org/backup.html
- SQLite WAL: https://www.sqlite.org/wal.html

OpenNGC by Mattia Verga is licensed CC-BY-SA-4.0. See `Catalogue_Notice.md` and `OpenNGC_README.md` for the selected-column derivative's attribution and provenance.

## Release 1.7.2 / package 1.7.2.1

Mosaic collections and panel exports, canonical object IDs/common names, grouped
library exposure summaries, import review and filtered retries, audited reimport
permissions, searchable offline help and package notes/progress during updates.


## Release 1.8.0 / package 1.8.0.1

- Extends the existing preview/import/processor/mosaic setup with format readers and explicit HDU/page/frame selection.
- Adds scientific raster/XISF conversion, SER frame previews and optional compressed-image backends, while preserving byte-for-byte original exports.
- Separates camera identity, gain units, readout/ROI/offset and optical configuration; retains provenance, normalization, reviewed re-detection and overrides.
- Explains calibration decisions and prepares exposure-specific dark-flat recipes.
- Preserves adjacent recognized sidecars and existing database records without destructive migration.

Native CFITSIO and Zstandard are optional local components, not bundled in the standard installer. JPEG/processed data and undecoded RAW/video remain original-file exports.
