# AstroArchive 1.2.0

Portable Windows 10/11 x64 WPF app for archiving smart-telescope FITS and creating verified stacking projects. Open `AstroArchive.exe`; read `Quick_Start.txt` for the complete workflow.

## Changes in 1.2.0

- Single-pass streamed discovery through a 128-file bounded queue, cached enumeration attributes and shared session JSON. Live Import/Repository rows, stage performance, files/s, processed MB/s and ETA.
- Indexed SQLite source manifest with path, file identity, size, modification/change times, SHA-256, destination, metadata and completion status. Stable verified NTFS/ReFS files can skip content reads; cloud/unsupported metadata never establishes a duplicate.
- Source hashing during temporary copying, destination verification, coordinated transactions and guarded source removal. Limited retries isolate per-file failures. Disk-full errors stop safely with committed copies retained.
- A local working SQLite index with FULL synchronous rollback-journal transactions and verified portable snapshots avoids an active database on a provider-backed archive. Snapshot backups use SQLite's backup API and self-contained rollback-journal destinations. Error reports include paths and native/SQLite codes.
- Adaptive 1/2/4/8-worker trials use real import batches, starting at 1 and retaining a faster result. Fixed worker counts and Retune are available. Workload and cache effects apply.
- Solving and rotation are off by default. Import selects Off, Ambiguous only or All, warns about runtime, and runs analysis after copying. Representative/session analysis and content-validated caches avoid unnecessary repetition.
- Explicit streamed/cloud source mode separates provider read/download waiting from local copying. Nonredirecting cloud placeholder directories are allowed; directory symlinks/junctions are skipped. Offline inputs avoid on-demand download delays.
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

The UI smoke option creates synthetic rows, checks filtering/defaults and renders the interface without importing user files. The console suite creates isolated generated-FITS data and an ASTAP protocol double. See `Validation.txt` for the packaged build's results. Windows-native file identity/deletion, WPF interaction and live Google Drive could not be tested in the Linux build environment. Real telescope captures and authenticated plate solving were unavailable. The protocol double does not solve real astronomical images.

## Performance and failure handling

Stage seconds are active wall time with overlapping worker scopes merged per stage; stage times are not additive. Byte counters describe consumed app data, not physical disk or network traffic. Gzip header/pixel parsing counts decoded bytes. External ASTAP/star-database reads are not observable. Cloud availability includes provider opens/reads and can include cached reads, not solely downloads. ETA applies to the active phase and becomes available after measurable progress.

Manifest shortcuts require verified content plus unchanged trusted identity/change metadata on both source and archive copy. Name, size and mtime alone are insufficient. New source files are fully hashed during copying. Destination bytes are read back before the record is committed. Original deletion adds durable indexing, final stamp checks and locked-handle physical-boundary/hash checks. Originals remain on failed cleanup, and duplicate originals are retained.

The previous user's time-dependent import failure could not be reproduced against their actual PC or diagnosed without its error. The code remedies batch-wide aborts, transient lock handling, cloud hydration state checks and provider-backed active SQLite. It logs remaining failures for diagnosis instead of silently reporting success.

## File and classification scope

Supported FITS suffixes: `.fit`, `.fits`, `.fts`, and `.gz` variants. Primary/IMAGE HDUs; integer/floating pixels, mono/CFA and up to four colour planes. Tile-compressed `.fz`, multi-frame cubes and non-FITS data require conversion. DWARF factory PNG calibration is outside this FITS archive; converted files need camera metadata or preserved `cam_0`/`cam_1` structure. Dimensions/binning alone do not identify the camera. Unknown make/model, conflicting channels and uncertain target intent remain for review.

Metadata corrections may move archived copies without modifying FITS pixels/headers. Exports create new folders and do not overwrite projects. Stacking itself is performed in external software.

## Source layout

- `Model.cs`, `FileState.cs`: metadata, settings, file identity/change stamps and cloud/reparse handling.
- `Fits.cs`, `Classification.cs`, `InstrumentDetection.cs`, `CameraDetection.cs`: parsing, catalogue, filename/header/structure classification and session metadata cache.
- `Repository.cs`, `ImportEngine.cs`, `FileTransfer.cs`, `Pipeline.cs`: SQLite index/manifest, bounded discovery/copy pipeline, hashing, retries, worker tuning and telemetry.
- `SourceCleanup.cs`, `ArchiveReset.cs`: verified source cleanup and scoped archive deletion.
- `Rotation.cs`, `PlateSolve.cs`: star matching, mount inference, ASTAP/Astrometry.net and candidate matching.
- `Export.cs`: session-aware verified projects and calibration safeguards.
- `App.cs`, `MainWindow.xaml`, `NativeFolderPicker.cs`, `Assets/`: WPF interface, native selectors and embedded branding.
- `Tests.cs`, `MockAstap.cs.txt`, `test.ps1`, `build.ps1`: generated-data checks and Windows build.

## External documentation and catalogue

- ASTAP: https://www.hnsky.org/astap.htm
- Astrometry.net API: https://astrometry.net/doc/net/api.html
- DWARF file/camera layout: https://help.dwarflab.com/en/docs/How-to-View-and-Obtain-the-Files-on-DWARF-3
- Google Drive streaming/offline modes: https://support.google.com/drive/answer/13401938
- SQLite backup API: https://www.sqlite.org/backup.html
- SQLite WAL: https://www.sqlite.org/wal.html

OpenNGC by Mattia Verga is licensed CC-BY-SA-4.0. See `Catalogue_Notice.md` and `OpenNGC_README.md` for the selected-column derivative's attribution and provenance.
