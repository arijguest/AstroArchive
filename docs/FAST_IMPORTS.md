# Repeat telescope imports

This page describes local-folder and USB imports. For the network picker and
live imports, see [network telescope imports](REMOTE_IMPORT.md). Network captures
are downloaded to a local cache before the full scan and verified archive import;
live mode watches new captures by default rather than skipping dated sessions.

Folder scans default to filename matching. The selected USB import flow described
below always performs a full scan; unscoped engine uploads retain filename matching.
The saved physical telescope scopes the inventory. Known files bypass image
opens, header/pixel reads, timestamps, companion metadata, destination stats and
hashing. Existing names are indexed in a compact SQLite table; older archives
backfill it once from their saved records without reading archived image files.
Archive deletion/explicit reimport decisions remain authoritative. Fast scans do
not hash every new candidate merely because the archive has deletion history.
Unrecognised candidates remain provisional until verified import checks their
content hash against that history, including renamed copies of deleted captures.
Full scans retain the scan-time content check.
Explicit reimport permission revisits the affected DWARF session rather than
omitting the file along with its already archived neighbours.

A dated DWARF_RAW session folder with a matching archived filename is skipped as
a whole. Up to eight saved filename probes locate an anchor; after a match the
folder is not enumerated. Its later additions and nested contents are intentionally
omitted. Counts describe saved archive names, not a fresh inventory of that folder.
A folder name alone does not establish a match. New dated sessions using
raw_001.fit remain candidates because sequential names are scoped to their session.

Seestar target/sub directories and other layouts keep enumerating names, skip
matches and inspect only new captures. This preserves discovery of new exposures
in reused target folders. Generic numbered names are folder scoped; names with
capture dates and other specific names can match across mirrors/drive-letter changes.
Bookkeeping directories are excluded, and unknown/newer folder trees go first.

**Settings > Import > Robust file matching (slower)** is persisted and off
by default. It bypasses filename/session/header shortcuts and reads headers and
content hashes, including archive-copy verification. Use it for same-name edits,
updated sidecars, additions to omitted sessions, or restoration of missing/damaged
copies. **Import → Review and repair → Full rescan** supplies that check for one manual scan.
This setting applies to folder scans and connected imports; Dump keeps verified
copy/cleanup behavior. Newly copied files retain SHA-256 readback verification,
and fast-skipped originals never enter source cleanup.

Detected storage adds **Import from Seestar…** or **Import from Dwarflab…** directly
to the Import menu and connected-telescope dropdown/button. Multiple devices of
the same make include their profile or drive to distinguish them. Saved volume
bindings win, then the currently selected compatible profile, then a unique
profile for the detected make. Ambiguous/unconfigured devices are configured in
the selection dialog with a physical device name.

The picker combines selected folders and individual files inside the telescope
volume. Paths outside that volume, linked items and system/application folders
cannot be selected. Folder selections are recursive; overlapping selections are
normalised. It remembers relative selections in the saved profile for the same
volume, preserving the common drive root for ancestor metadata lookup. Opening
or cancelling the picker performs no recursive scan or import.

Small selected USB imports start directly. Selections containing 500 or more
capture files, including files inside selected folders, show a compact confirmation
with the batch size and **Import / Cancel** buttons. This flow always bypasses filename,
DWARF-session and header-cache shortcuts and checks hashes/archive copies before
screening and verified import. Existing ignore/rejection/deletion policies remain
applicable; originals stay and plate solving/rotation stay off. Results state the
selected scope instead of declaring the entire repository up to date.

Generated-data checks cover deliberately edited matching names, bounded session
omission, fresh DWARF sessions with repeated filenames, Seestar additions, mirror
moves, telescope scoping, legacy index migration, deletion/reimport decisions,
robust repair and device selection. A 1,000-known/one-new fixture reads only the
new 2,880-byte header. These counters measure application reads rather than USB
bus traffic. Native WPF checks cover button visibility, profile selection, optional
robust matching and light/dark rendering. Physical device arrival and throughput
still need validation on connected hardware.
