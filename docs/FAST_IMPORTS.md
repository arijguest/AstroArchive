# Repeat telescope imports

Folder scans and connected Seestar/DWARF imports default to filename matching.
The saved physical telescope scopes the inventory. Known files bypass image
opens, header/pixel reads, timestamps, companion metadata, destination stats and
hashing. Existing names are indexed in a compact SQLite table; older archives
backfill it once from their saved records without reading archived image files.
Archive deletion/explicit reimport decisions remain authoritative.

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

**Import > Import options > Robust file matching (slower)** is persisted and off
by default. It bypasses filename/session/header shortcuts and reads headers and
content hashes, including archive-copy verification. Use it for same-name edits,
updated sidecars, additions to omitted sessions, or restoration of missing/damaged
copies. **Full rescan of source** supplies that check for one manual scan.
This setting applies to folder scans and connected imports; Dump keeps verified
copy/cleanup behavior. Newly copied files retain SHA-256 readback verification,
and fast-skipped originals never enter source cleanup.

The visible **Import from ...** button appears when telescope storage is detected.
Saved volume bindings win, then the currently selected compatible profile, then
a unique profile for the detected make. Ambiguous profiles require selection;
an unconfigured telescope requires a saved physical device ID. Multiple connected
sources are offered in a menu. The action retains originals and leaves plate
solving/rotation off.

Generated-data checks cover deliberately edited matching names, bounded session
omission, fresh DWARF sessions with repeated filenames, Seestar additions, mirror
moves, telescope scoping, legacy index migration, deletion/reimport decisions,
robust repair and device selection. A 1,000-known/one-new fixture reads only the
new 2,880-byte header. These counters measure application reads rather than USB
bus traffic. Native WPF checks cover button visibility, profile selection, optional
robust matching and light/dark rendering. Physical device arrival and throughput
still need validation on connected hardware.
