# Mosaic collections

AstroArchive identifies mosaic captures from metadata and organises them as
named collections with stable panel identities. Images retain their target,
Light/Stack classification, telescope, night, session and canonical archive path.
A capture can be linked to several collections without creating additional copies.
Completed stitched images have an Output role separate from panel inputs.

## Use

New imports preserve mosaic hints while reading the existing FITS header and
shotsInfo.json. Explicit mosaic and panel information creates **Declared**
memberships. A MOSAIC marker without sufficient panel information creates a
**Suggested** collection with unresolved members. No solver or image decoding
runs for mosaic detection during import.

In **Mosaics**, choose **Detect from metadata** to examine the selected Library
captures, or the visible Library captures if none are selected. This reuses
indexed metadata and filename/folder information. Reliable existing sky mappings
can suggest panels and show their outlines. Geometric suggestions require review.
Identical target names or target-centre RA/Dec values alone do not establish a mosaic.

For older indexed captures, **Tools > Read headers/session metadata** reads the
archived FITS headers and preserved session JSON. It checks archive identity first;
verification may need hashing when file identity cannot establish unchanged content.
It does not decode image pixels or call a solver.

Select captures in Library and right-click **Mosaics > Assign to panel or completed
output**, or use **Assign library selection** in the Mosaics tab. Create a collection
or choose an existing one, then choose/create its panel. Attach a completed
stitched image using the output checkbox. A collection may span targets, nights,
filters and cameras. Calibration frames remain in their existing category.

Use the panel list to show individual panels, unresolved captures or outputs.
Select suggested members with known panels and choose **Confirm selected**.
Unresolved captures must first be assigned to a panel. Tools can create an empty
planned panel, rename panels, edit a collection's name/planned count, or dismiss
a collection. Removing members/dismissing a collection retains the user decision
on subsequent detection. It does not delete archive captures.

Library/import Filters include Mosaic, Panel and Mosaic state. Collection names
and panel labels are searchable; CSV catalogues include mosaic/panel labels.
The import view initially shows source hints, while Library shows saved collections.

## Optional solving

**Tools > Solve selected representatives** explicitly runs the configured solver.
It chooses one selected representative for each known panel; unassigned images
are solved individually because their common pointing is unknown. Existing
content-validated solver caches are reused. No target labels are replaced and
solutions are not propagated to unrelated exposures.

ASTAP solutions can retain their complete WCS from solver output. Astrometry.net
can retrieve the solved WCS for its sampled image. Solved centres without a complete
mapping remain centre-only. A configured field height can provide an approximate
footprint, labelled as such, with orientation assumed. The online option sends
detected star coordinates using the existing configured account.

## Supported metadata and uncertainty

The FITS reader accepts MOSAICID, MOSNAME/MOSAICNM, MOSAIC,
PANELID/TILEID, PANELROW plus PANELCOL, NPANELS and MOSROLE.
The generic session reader accepts mosaicId, mosaicName, isMosaic,
panelId/tileId, panelRow/panelColumn, panelCount/plannedPanels and
mosaicRole, including documented snake-case alternatives in the parser.
A nested mosaic object can supply id and name.

Session arrays of panels/frames/images are interpreted only when a unique entry
associates the current filename through file/filename/path/files. A panel array's
entry ID identifies that panel; an arbitrary frame ID does not. The original
session JSON is retained. Conflicting identifiers or ambiguous filename entries
stay Suggested, with the conflict shown in the membership evidence.

The DWARF DWARF_RAW_...MOSAIC... convention and explicit panel_01/tile_01
tokens are recognised. Filenames alone produce suggestions. A named mosaic
folder provides a shared scope for its panel subdirectories. Explicit project IDs
are scoped to the telescope's stable identity and can span nights. Weak markers
are conservatively scoped to their source folder/session.

Geometry supports undistorted RA/Dec TAN mappings in degrees, with ICRS/FK5
J2000 coordinates, reference pixels, complete CD matrices or CDELT with PC/CROTA.
Image corners and the actual centre are projected onto the sky; CRVAL alone is not
assumed to be the image centre. Unsupported distortions/projections/epochs remain
without a trusted footprint.

Pointings within 8% of the smaller field dimension are conservatively grouped as
one panel, using a bound that avoids joining a long chain of drifts. Geometry-only
candidates stay within a target/camera/capture-session scope and connect fields
whose footprint overlap is 5–85% of the smaller footprint. This is evidence of
adjacent fields, not proof of mosaic intent: every geometric membership remains
Suggested until confirmed. Sessions with more than 128 distinct fields require a
smaller selection. Stable collection/panel IDs survive new exposures and renames.

## Portable repository and exports

Collections and content-hash memberships are stored in the local SQLite index and
its portable .astroarchive/index.sqlite snapshot. A schema-versioned
.astroarchive/mosaics/manifest.json also preserves collection/panel IDs, states,
roles, geometry, hashes and relative archive paths. If an index is lost, this
manifest restores the collections and reindexing reconnects captures by hash.
Missing archive records remain visible. Deleting captures/resetting the archive
retains collection definitions, alongside deletion history.

Malformed/unsupported manifests are retained with a warning; they are not
silently overwritten during recovery. Checkpoints refresh paths after metadata
refiling or telescope renaming. Manual membership and ignored-member records
remain authoritative during rediscovery.

**Export collection** prepares independent stacking inputs per panel, preserving
the existing target/camera/filter/exposure/gain/dimensions/calibration separation.
Suggested/unresolved membership must be reviewed first. General stacking exports
also separate saved mosaic panels. If captures belong to several mosaics, choose
the intended collection explicitly.

Exports contain mosaics/name_id/panels/panel_id/...,
workflow notes, panel-result/stitched output locations, and collection/membership
provenance in manifest.json. In Stacks/Both mode, linked completed mosaics go
in outputs/completed, separate from contributing captures. Source files remain
byte-for-byte copies. Stacking/stitching runs in external software.

## Validation

Generated-data regressions cover metadata-only parsing, DWARF markers, structured
session arrays, conflicts, rotated/RA-wrap footprints, unsupported WCS, dithering,
declared imports across nights, manual/shared collections, stable identities,
ignored decisions, panel-aware exports, output separation and manifest recovery.
Native WPF smoke checks cover hierarchy, evidence, chooser bindings, selection
controls, library filters, outlines and light/dark rendering.

Real Seestar/DWARF mosaic exports and authenticated solver service responses
remain hardware/provider validation. Generic metadata conventions and generated
fixtures do not establish support for every firmware-specific session schema.
