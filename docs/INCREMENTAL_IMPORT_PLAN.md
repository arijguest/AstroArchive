# Final plan: faster repeat telescope imports

Status: implementation plan, grounded in the current source and `FAST_IMPORTS.md`.
No importer behaviour has been changed by this document.

Implementation update: the USB folder/file picker, remembered volume-relative
selection, branded Seestar/Dwarflab actions and full-scan confirmation are now
implemented on `temp`. That selected USB flow always uses robust/full matching.
The durable folder-state and automatic incremental omission work below remains
planned; no source-side cache or timestamp omission has been enabled.

## 1. Objective and firm decisions

Make repeat imports avoid unnecessary directory enumeration while retaining the
existing verified transfer, deletion history, explicit reimport and repair paths.
Discovery coverage and content verification are separate promises.

- Reuse the existing SQLite working index and its portable archive checkpoint.
- Add selection of multiple capture folders before discovery, shared by manual
  scans and connected imports. This is the first and most dependable Seestar gain.
- Add durable folder discovery state to that index; do not replace existing
  per-file import history or copy verification.
- Automatically skip folder contents only with an authoritative, continuous
  change signal. Directory timestamps are hints, including on a filesystem with
  reliable individual file stamps.
- Permit timestamp-based folder omission only in a separate, explicit accelerated
  discovery option, after physical-device validation. It cannot promise immediate
  discovery of every capture.
- Do not write an import cache onto the telescope in the first release. Consider
  an optional portable snapshot later, for sharing inventory between computers.
- Retain Robust file matching and Full rescan; both override discovery omission.
  Never use last-import time as an exclusion cutoff.

On FAT/exFAT without a producer-maintained catalogue or change feed, exhaustive
discovery requires enumeration of the selected folders. A cache written only by
AstroArchive cannot know what the telescope added while disconnected.

## 2. What already exists

| Component | Implemented behaviour | Treatment in this plan |
| --- | --- | --- |
| `Repository.cs`, `FilenameScanCache.cs` | Telescope-scoped `import_names` inventory, one-time legacy backfill, known-name skips, sequential names scoped by folder/session | Reuse; add discovery scope and avoid loading unrelated source history where possible |
| `FilenameScanCache.cs` | A dated DWARF session can be omitted after one of up to eight archived filename probes succeeds | Retain as a documented fast-policy shortcut; add completion/pending-work guards |
| `SessionScanCache.cs`, `SourceHistory.cs` | Source inventory checks, volume/telescope matching, sidecar dependencies, missing-copy checks; history across parent/session browse roots | Reuse these checks and path handling where applicable; this inventory path is distinct from the normal UI's filename-matching path |
| `FileState.cs` | File identity/stamps; reliable individual-file change checks on supported Windows filesystems; volume identification | Reuse; do not infer recursive directory stability from individual-file reliability |
| `ScanSupport.cs`, `Repository.cs` | Bounded metadata queue, local spill file, header cache, unknown/newer trees prioritised | Keep bounded processing; spill files are temporary, not durable resume records |
| `ImportEngine.cs` | Source-change checks, copy hashing, SHA-256 readback, transactional per-file completion, failure manifests and portable checkpoints | Preserve; add folder-resolution updates alongside existing completion commits |
| `TelescopeConnections.cs`, `TelescopeUi.cs` | Saved stable telescope identity, volume-relative source binding, USB reconnect/cancellation, one-click upload | Extend with remembered folder scope; preserve original-retention defaults |
| `ImportOptionsUi.cs`, `AutoUpload.cs`, `App.cs` | Robust matching is persisted, off by default; manual/USB robust paths perform full reads and hashing | Preserve its meaning and make all new shortcuts subordinate to it |
| `Pipeline.cs`, import reports | Timed discovery/metadata/copy stages and existing skip counters | Extend to measure actual directory work and state coverage accurately |

The current fast Seestar path already avoids image reads for known names, but
enumerates reused target folders. The current DWARF shortcut deliberately misses
later additions and can omit unresolved neighbours once any file is archived.
Existing tests document those tradeoffs; the new plan must not claim that cached
counts or matching names constitute fresh discovery or checksum verification.

## 3. Folder selection before discovery

Introduce one import-scope model used by `App.Scan` and `UsbAutoUpload.Run`:
one source anchor, selected relative folders and an explicit All-folders flag.
Keep the source anchor stable when selecting a child so classification and
ancestor metadata lookup retain their context.

- Add **Choose capture folders...** and a visible current-scope summary.
- Present a lazy directory tree from recognised Seestar/DWARF layouts and cached
  children. Do not recursively inventory images, calculate file counts or read
  headers to populate it. Directory-only listing may still read filesystem
  directory entries internally; measure that cost rather than assuming it is free.
- Allow multiple selections, expand on demand and remove overlapping selections.
- Offer related subs/stacks together only for recognised layouts, with the included
  folders visible; leave unknown layouts as ordinary directory selections.
- Remember the selection per stable telescope identity and bound source volume.
  Display it before both manual and one-click imports. Fresh/legacy profiles keep
  All folders until the user makes a narrower choice.
- Offer Previous selection and All folders. Show newly discovered target folders
  in the picker without silently adding them to a remembered restricted scope.
- Put this persistent hint beside source selection: “Import faster: choose the
  target folders you captured into. Scanning the whole telescope also checks
  older captures.” A hint alone is not sufficient; selection must affect traversal.
- A missing selected folder is visible and is not silently replaced by the whole
  drive. Changed volume/source bindings require review or the normal discovery
  fallback. Changed choices invalidate the existing scan plan.

Validate paths using existing containment/link rules, including source/archive
separation. Keep the common source anchor for cleanup validation; skipped and
out-of-scope files never enter deletion candidates. Import-grid filters remain
separate from pre-discovery folder scope.

## 4. Durable folder state in the existing index

Add versioned, indexed source/folder records to the existing working database.
They are automatically included in the existing portable archive checkpoint.
The destination repository provides the archive boundary; no telescope-wide
import-complete flag may substitute for that archive's records.

Store stable telescope identity, bound source identity, source anchor and folder
relative path; known child folders; last complete inventory generation; observed
folder indicators; policy/classification version; unresolved discovery/import
work; scan errors; last complete discovery and successful-import times; and, if
supported, the change-provider identity/epoch/cursor.

Reuse `source_manifest`, `import_names` and deletion records for successful imports
and decisions. Persist lightweight pending discovery rows only where existing
manifests cannot represent a newly discovered/unparsed candidate. Avoid duplicating
all FITS metadata or creating a second independent full-file database.

Treat these as separate facts:

1. Inventory complete: enumeration finished successfully at a recorded generation.
2. Work resolved: every discovered candidate has a committed result or an explicit
   exclusion under the recorded import policy.
3. Discovery current: authoritative evidence covers changes since that inventory.

Neither of the first two implies that the telescope has finished adding captures.
Failed, unreadable, unparsed, unselected-for-import and review-pending captures
remain revisit-able. Explicit exclusions are policy-dependent; changing failed-name,
raster, review, reimport or matching choices must revisit affected work.

Use additive schema migration. Old per-file history remains useful, but does not
prove a complete folder inventory; establish that baseline during a subsequent
actual enumeration. A malformed/newer cache cannot authorise omission.

## 5. Discovery decision and scan algorithm

Keep discovery confidence separate from the existing filename-versus-robust
matching choice. In the first release, Seestar uses complete enumeration within
the chosen scope and its existing fast per-file matching. It gains no new silent
timestamp omission. Existing DWARF fast-policy omissions remain explicitly reported.

For each scan:

1. Resolve telescope, source and selected scope. Snapshot options and validate paths.
2. Load relevant archive history and pending folder work once. Prioritise unresolved
   work and new trees, retaining the existing bounded queue/spill approach.
3. When a trusted provider is available, validate its identity, history continuity
   and coverage, then identify changed files/folders and dependency changes.
4. Enumerate selected folders that are new, changed, unresolved or lack trustworthy
   change evidence. An authoritative unchanged record can avoid enumeration.
5. In optional accelerated discovery only, completed folders with matching hints
   may be omitted. Check every relevant cached directory directly; a parent stamp
   cannot stand for descendants. Treat unknown indicators as requiring enumeration.
6. Send candidates through the existing filename matching or robust pipeline.
   Existing name matching still deliberately trusts names and can miss same-name
   rewrites or missing archive copies; only robust/full checks promise repair.
7. Commit a new inventory generation only after enumeration completes. Changes
   detected during scanning leave the folder pending. With unreliable indicators,
   do not claim a live telescope is stable merely because before/after values match.
8. Commit import outcomes through the existing verified transactional pipeline.
   Keep unresolved work durable before advancing a discovery cursor.

For authoritative providers, bootstrap a baseline with change capture covering
the whole enumeration, replay concurrent changes, and commit the cursor together
with pending work. Journal reset/truncation, dropped events, catalogue uncertainty
or unavailable access trigger enumeration. A catalogue must cover files, renames,
deletions, nested folders and relevant metadata dependencies. A Windows watcher
alone cannot cover telescope activity while disconnected.

Provider support is a later capability extension: investigate real Seestar
catalogues first; implement journal support only for supported filesystem/access
combinations. Neither is assumed available or required for the first release.

## 6. DWARF guards and optional accelerated discovery

Keep the existing dated-session recognition and bounded anchor lookup, but allow
whole-session fast omission only after a complete inventory and resolution of
all discovered work under the current policy. Do not infer this from one imported
file. Known failures, pending review, explicit reimport and incompatible policy
changes prevent omission. Legacy sessions need one enumeration to establish it.

Even a fully imported DWARF session can receive later files. Without an authoritative
change signal, omission remains a fast-policy assumption. Report it as such and
retain robust/full enumeration. Update affected existing tests intentionally.

Before releasing timestamp-based Seestar omission, test actual supported models,
firmware and storage layouts during capture, repeat observations, nesting, rename,
deletion, clock correction and reconnection. Expose it as **Accelerated folder
checks**, off by default, separately from Robust file matching.

Use a persisted reconciliation queue for apparently unchanged folders. Check at
least one queued folder per accelerated scan, always advancing the queue; when
there are no more than five, check all. Continue additional checks within a
measured small work budget. Prioritise oldest unchecked folders. With a fixed set
of N folders, at least one completed check per scan gives an upper bound of N
completed scans, not a wall-clock guarantee. A discrepancy disables timestamp
omission for that source and triggers a normal selected-scope scan. Robust matching
and Full rescan always override it. Heuristic reconciliation never becomes proof
of immediate complete discovery.

## 7. Recovery, portable caches and precise results

Keep SQLite transactions and existing archive checkpoints as the durability
boundary. Cancellation/disconnection retains committed copies and pending work;
it must not mark incomplete inventory as current. Local spill files can be deleted
and regenerated. Source changes are revalidated by the existing import pipeline.
The last-import timestamp is informational, not a watermark for file exclusion.

Source-volume serials and stored paths are useful bindings, not collision-proof
device identities. Never authorise a new omission from a volume serial alone;
require compatible provider identity/baseline or re-establish discovery. A different
destination archive evaluates its own import history. Telescope rename preserves
the existing stable `SessionIdentity`. Restored/moved archives may keep import
history but must revalidate source bindings and any discovery cursors.

Do not automatically create a cache in Seestar capture folders. If cross-computer
reuse warrants a later source-side snapshot, use a dedicated excluded application
directory, a versioned bounded format, relative paths, an integrity check and
replace-safe writes. Read-only storage and write failures remain fully supported.
The snapshot is advisory: an integrity check detects corruption, not freshness;
import receipts are reconciled against the destination archive. Validate firmware
compatibility before enabling writes. Existing portable archive indexes already
cover moving the same archive between computers.

Extend reports with selected/all-folder scope; folders enumerated; entries examined;
known names skipped; folders omitted by authoritative evidence; folders omitted
by fast-policy assumptions; pending work; fallback reasons; and reconciliation work.
Counts for unenumerated folders are explicitly cached estimates, not fresh totals.
Do not generate misleading enumeration throughput by crediting cached names as
freshly discovered entries.

Replace the current unconditional USB “Repository is up to date” wording. Use
“No new filenames found in the selected folders” for normal name matching, and
“Quick check finished; X folders skipped without enumeration” for heuristic
coverage. Robust results state their scope and errors. Full rescan applies to the
visible scope; All folders is the explicit whole-source choice.

## 8. Implementation order and release gates

| Order | Deliverable | Main code integration |
| --- | --- | --- |
| 1 | Scope model, multi-folder picker, remembered selection, source hint and accurate result wording | `Model.cs`, `TelescopeConnections.cs`, new scope/UI partials, `MainWindow.xaml`, `App.cs`, `AutoUpload.cs`, `TelescopeUi.cs`, `NavigationUi.cs` |
| 2 | Folder schema, inventory generations, durable unresolved work, DWARF guards and cache invalidation | `Repository.cs`, new folder-state partial, `ImportEngine.cs`, `FilenameScanCache.cs`, `SourceHistory.cs` |
| 3 | Discovery counters, existing help/documentation updates and Windows regression/UI validation | `Pipeline.cs`, reports, `Quick_Start.txt`, `WalkthroughUi.cs`, `FAST_IMPORTS.md`, tests |
| 4 | Hardware validation; optional accelerated checks and reconciliation only if useful | `FileState.cs`, new discovery strategy, import options, real-device measurements |
| Later | Authoritative catalogue/journal provider; optional source-side portable snapshot if justified | Discovery strategy interface and source adapters |

Implement steps 1–3 as the first release. Its immediate speed benefit is scoped
discovery and reuse of the existing per-file cache; it does not depend on unproven
Seestar timestamp behaviour. Keep C# 5/.NET Framework 4.8 and the existing no-new-
runtime build constraints. Add new engine/test files to `test.ps1`'s explicit source
list; the application build already collects non-test C# files.

Required generated-fixture checks:

- Reused Seestar target receives a new exposure; new/nested folders; repeated
  numbered names; old-dated files copied in later; same-name rewrite in robust mode.
- Only selected branches are traversed; overlapping selections processed once;
  metadata ancestry preserved; missing selections and scope changes visible.
- Cancellation/disconnection after partial enumeration/import; failed neighbours
  do not disappear behind a DWARF shortcut; unimported/review candidates survive.
- Explicit deletion/reimport, missing/damaged copies under robust/full checks,
  policy/version changes, legacy migration and corrupt folder state.
- Drive-letter changes, source rebinding, telescope rename, moved/restored or
  different destination archives, and invalid provider history.
- Full rescan bypasses all omission; fast-skipped originals never reach cleanup;
  counters distinguish real work, cached estimates and verification.
- If optional acceleration is implemented: timestamp-preserving additions are
  eventually found through reconciliation; discrepancy disables the shortcut;
  no complete-discovery claim is made before authoritative evidence/enumeration.

Run the existing Windows application tests/build and WPF UI smoke checks as
documented in `DEVELOPMENT.md`. Extend `FastImportTests.cs`, `SessionScanTests.cs`,
`TelescopeTests.cs` and relevant UI smoke tests. Physical hardware validation is
separate from fixture tests; application byte counters are not USB traffic.

Benchmark discovery separately from copying on a populated Seestar. Compare the
current all-folder scan, selected-folder scan, unchanged repeat and optional
accelerated scan, using both cold and warm filesystem caches. For a representative
100-target fixture with two selected targets, unselected targets must have zero
content enumerations; examined capture entries should fall by approximately 98%
when folders are equally populated, excluding picker overhead. Discovery should
scale with selected/changed content rather than total drive contents. Report median
and slow-case timings on real hardware; do not promise a numerical speedup before
those measurements. If enumeration is dominated by one huge active target folder,
neither selection nor a stale cache removes that cost: seek an authoritative
catalogue or retain the exhaustive fallback.
